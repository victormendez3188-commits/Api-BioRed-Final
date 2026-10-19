using System.Security.Cryptography;
using BioRed.Application.Delivery;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Delivery;

internal sealed class DeliveryConfirmationService : IOrderDeliveryConfirmationService
{
    private readonly BioRedDbContext _dbContext;
    private readonly IPasswordHasher<CodigoEntregaPedido> _codeHasher;
    private readonly DeliveryConfirmationEmailSender _emailSender;
    private readonly DeliveryConfirmationOptions _options;
    private readonly ILogger<DeliveryConfirmationService> _logger;

    public DeliveryConfirmationService(
        BioRedDbContext dbContext,
        IPasswordHasher<CodigoEntregaPedido> codeHasher,
        DeliveryConfirmationEmailSender emailSender,
        IOptions<DeliveryConfirmationOptions> options,
        ILogger<DeliveryConfirmationService> logger)
    {
        _dbContext = dbContext;
        _codeHasher = codeHasher;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DeliveryCodeDispatchResult> GenerateForNewOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new(
                DeliveryConfirmationStatus.Disabled,
                Detail: "La confirmación mediante código está deshabilitada.");
        }

        var order = await FindOrderAsync(orderId, cancellationToken);
        return order is null
            ? new(DeliveryConfirmationStatus.NotFound, Detail: "No se encontró el pedido.")
            : await GenerateAndSendAsync(order, cancellationToken);
    }

    public async Task<DeliveryCodeDispatchResult> ResendAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new(
                DeliveryConfirmationStatus.Disabled,
                Detail: "La confirmación mediante código está deshabilitada.");
        }

        var order = await FindOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return new(DeliveryConfirmationStatus.NotFound, Detail: "No se encontró el pedido.");
        }

        var isOwner = actorType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
            order.CodCliente.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase);
        if (!isOwner && !isAdministrator)
        {
            return new(
                DeliveryConfirmationStatus.Forbidden,
                Detail: "Solo el cliente propietario o un administrador puede reenviar el código.");
        }

        if (!await IsActiveAccountAsync(accountId, actorType, actorReferenceId, cancellationToken))
        {
            return new(
                DeliveryConfirmationStatus.Forbidden,
                Detail: "La cuenta autenticada no coincide con el solicitante.");
        }

        if (order.EstadoPedido is "Entregado" or "Cancelado")
        {
            return new(
                DeliveryConfirmationStatus.InvalidState,
                Detail: "No se puede reenviar un código para un pedido Entregado o Cancelado.");
        }

        return await GenerateAndSendAsync(order, cancellationToken);
    }

    public async Task<VerifyDeliveryCodeResult> VerifyAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                DeliveryConfirmationStatus.Forbidden,
                Detail: "Solo el repartidor asignado puede validar el código de entrega.");
        }

        var order = await FindOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return new(DeliveryConfirmationStatus.NotFound, Detail: "No se encontró el pedido.");
        }

        if (string.IsNullOrWhiteSpace(order.CodRepartidor) ||
            !order.CodRepartidor.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase) ||
            !await IsActiveAccountAsync(accountId, actorType, actorReferenceId, cancellationToken))
        {
            return new(
                DeliveryConfirmationStatus.Forbidden,
                Detail: "El repartidor autenticado no está asignado a este pedido.");
        }

        if (!order.EstadoPedido.Equals("En Camino", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                DeliveryConfirmationStatus.InvalidState,
                Detail: "El código se valida únicamente cuando el pedido está En Camino.");
        }

        var confirmation = await _dbContext.CodigosEntregaPedido
            .SingleOrDefaultAsync(item => item.IdPedido == orderId, cancellationToken);
        if (confirmation is null)
        {
            return new(
                DeliveryConfirmationStatus.NotConfigured,
                Detail: "El pedido no tiene un código de entrega. Solicite al cliente o al administrador que lo reenvíe.");
        }

        if (confirmation.Validado)
        {
            if (confirmation.IdCuentaValidacion != accountId)
            {
                return new(
                    DeliveryConfirmationStatus.Forbidden,
                    Detail: "El código ya fue validado por otro repartidor.");
            }

            return new(
                DeliveryConfirmationStatus.Success,
                await BuildVerificationResponseAsync(order, confirmation.FechaValidacionUtc ?? DateTime.UtcNow, cancellationToken));
        }

        var nowUtc = DateTime.UtcNow;
        if (confirmation.FechaExpiracionUtc <= nowUtc)
        {
            return new(
                DeliveryConfirmationStatus.Expired,
                Detail: "El código de entrega venció. Solicite un reenvío al cliente o al administrador.");
        }

        if (confirmation.IntentosFallidos >= confirmation.MaxIntentos)
        {
            return new(
                DeliveryConfirmationStatus.Locked,
                Detail: "Se agotaron los intentos del código. Debe reenviarse un código nuevo.");
        }

        var verification = _codeHasher.VerifyHashedPassword(
            confirmation,
            confirmation.CodigoHash,
            code.Trim());

        if (verification == PasswordVerificationResult.Failed)
        {
            confirmation.IntentosFallidos++;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new(
                confirmation.IntentosFallidos >= confirmation.MaxIntentos
                    ? DeliveryConfirmationStatus.Locked
                    : DeliveryConfirmationStatus.InvalidCode,
                Detail: confirmation.IntentosFallidos >= confirmation.MaxIntentos
                    ? "Se agotaron los intentos del código. Debe reenviarse un código nuevo."
                    : "El código de entrega no es válido.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            confirmation.CodigoHash = _codeHasher.HashPassword(confirmation, code.Trim());
        }

        confirmation.Validado = true;
        confirmation.IdCuentaValidacion = accountId;
        confirmation.FechaValidacionUtc = nowUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            DeliveryConfirmationStatus.Success,
            await BuildVerificationResponseAsync(order, nowUtc, cancellationToken));
    }

    private async Task<DeliveryCodeDispatchResult> GenerateAndSendAsync(
        Pedido order,
        CancellationToken cancellationToken)
    {
        var configurationError = _emailSender.ValidateConfiguration();
        if (configurationError is not null)
        {
            return new(
                DeliveryConfirmationStatus.EmailConfigurationInvalid,
                Detail: configurationError);
        }

        var recipient = await
        (
            from account in _dbContext.CuentasAcceso.AsNoTracking()
            join client in _dbContext.Clientes.AsNoTracking()
                on order.CodCliente equals client.CodCliente
            where account.Estado &&
                  account.TipoUsuario == "Cliente" &&
                  account.ReferenciaId == order.CodCliente
            select new
            {
                account.Correo,
                ClientName = client.NombreCliente + " " + client.ApellidoCliente
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (recipient is null)
        {
            return new(
                DeliveryConfirmationStatus.NotConfigured,
                Detail: "El cliente del pedido no tiene una cuenta activa para recibir el código.");
        }

        var nowUtc = DateTime.UtcNow;
        var code = RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");
        var lifetime = Math.Clamp(_options.CodeLifetimeMinutes, 5, 1_440);
        var maxAttempts = Math.Clamp(_options.MaxAttempts, 3, 10);

        var confirmation = await _dbContext.CodigosEntregaPedido
            .SingleOrDefaultAsync(item => item.IdPedido == order.IdPedido, cancellationToken);
        if (confirmation is null)
        {
            confirmation = new CodigoEntregaPedido
            {
                IdPedido = order.IdPedido
            };
            _dbContext.CodigosEntregaPedido.Add(confirmation);
        }

        confirmation.FechaGeneracionUtc = nowUtc;
        confirmation.FechaExpiracionUtc = nowUtc.AddMinutes(lifetime);
        confirmation.IntentosFallidos = 0;
        confirmation.MaxIntentos = maxAttempts;
        confirmation.CodigoHash = _codeHasher.HashPassword(confirmation, code);
        confirmation.EstadoEnvio = "PENDIENTE";
        confirmation.IntentosEnvio++;
        confirmation.UltimoEnvioUtc = nowUtc;
        confirmation.DetalleFalloEnvio = null;
        confirmation.Validado = false;
        confirmation.IdCuentaValidacion = null;
        confirmation.FechaValidacionUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailSender.SendAsync(
                recipient.Correo,
                recipient.ClientName.Trim(),
                order.CodPedido,
                code,
                confirmation.FechaExpiracionUtc,
                cancellationToken);

            confirmation.EstadoEnvio = "ENVIADO";
            confirmation.DetalleFalloEnvio = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new(
                DeliveryConfirmationStatus.Success,
                new DeliveryCodeDispatchResponse(
                    order.IdPedido,
                    order.CodPedido,
                    confirmation.FechaExpiracionUtc,
                    confirmation.EstadoEnvio));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            confirmation.EstadoEnvio = "FALLIDO";
            confirmation.DetalleFalloEnvio = "No fue posible enviar el código de entrega por correo.";
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                exception,
                "No fue posible enviar el código de confirmación del pedido {OrderId}.",
                order.IdPedido);

            return new(
                DeliveryConfirmationStatus.EmailDeliveryFailed,
                new DeliveryCodeDispatchResponse(
                    order.IdPedido,
                    order.CodPedido,
                    confirmation.FechaExpiracionUtc,
                    confirmation.EstadoEnvio),
                "El pedido fue creado, pero no se pudo enviar el código de entrega.");
        }
    }

    private async Task<DeliveryVerificationResponse> BuildVerificationResponseAsync(
        Pedido order,
        DateTime verifiedAtUtc,
        CancellationToken cancellationToken)
    {
        var clientName = await _dbContext.Clientes.AsNoTracking()
            .Where(item => item.CodCliente == order.CodCliente)
            .Select(item => item.NombreCliente + " " + item.ApellidoCliente)
            .SingleAsync(cancellationToken);

        var items = await
        (
            from detail in _dbContext.PedidosDetalle.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdPedido == order.IdPedido
            orderby detail.IdPedidoDetalle
            select new DeliveryVerificationItemResponse(
                detail.CodProducto,
                product.NombreProducto,
                detail.Cantidad,
                detail.PrecioUnitario,
                detail.Subtotal,
                detail.VendidoPorUnidad,
                detail.UnidadBase)
        ).ToArrayAsync(cancellationToken);

        var isPaid = await _dbContext.PagosPedidos.AsNoTracking()
            .AnyAsync(
                item => item.IdPedido == order.IdPedido && item.Estado == "COMPLETED",
                cancellationToken);

        return new DeliveryVerificationResponse(
            order.IdPedido,
            order.CodPedido,
            clientName,
            order.CodTipoPago,
            isPaid,
            order.TotalCobrado,
            isPaid ? 0m : order.TotalCobrado,
            "GTQ",
            items,
            verifiedAtUtc);
    }

    private Task<Pedido?> FindOrderAsync(int orderId, CancellationToken cancellationToken) =>
        _dbContext.Pedidos.SingleOrDefaultAsync(
            item => item.IdPedido == orderId,
            cancellationToken);

    private Task<bool> IsActiveAccountAsync(
        int accountId,
        string actorType,
        string actorReferenceId,
        CancellationToken cancellationToken) =>
        _dbContext.CuentasAcceso.AsNoTracking().AnyAsync(
            item => item.IdCuenta == accountId &&
                    item.Estado &&
                    item.TipoUsuario == actorType &&
                    item.ReferenciaId == actorReferenceId,
            cancellationToken);
}
