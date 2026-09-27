using BioRed.Application.Payments;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Payments;

internal sealed class PaymentReceiptService : IPaymentReceiptService
{
    private static readonly ReceiptDeliveryMethod[] DeliveryMethods =
    [
        ReceiptDeliveryMethod.DownloadPdf,
        ReceiptDeliveryMethod.Email
    ];

    private readonly BioRedDbContext _dbContext;
    private readonly PaymentReceiptPdfGenerator _pdfGenerator;
    private readonly PaymentReceiptEmailSender _emailSender;

    public PaymentReceiptService(
        BioRedDbContext dbContext,
        PaymentReceiptPdfGenerator pdfGenerator,
        PaymentReceiptEmailSender emailSender)
    {
        _dbContext = dbContext;
        _pdfGenerator = pdfGenerator;
        _emailSender = emailSender;
    }

    public async Task EnsureForPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.PagosPedidos
            .SingleOrDefaultAsync(item => item.IdPago == paymentId, cancellationToken);

        if (payment is null || payment.Estado != "COMPLETED" ||
            await _dbContext.ComprobantesPago.AsNoTracking().AnyAsync(
                item => item.IdPago == paymentId,
                cancellationToken))
        {
            return;
        }

        var email = await GetRegisteredEmailAsync(payment.CodCliente, cancellationToken);
        var paidAt = payment.CompletadoElUtc ?? payment.CreadoElUtc;
        var receipt = new ComprobantePago
        {
            IdPago = payment.IdPago,
            NumeroComprobante = $"REC-{paidAt:yyyyMMdd}-{payment.IdPago:00000000}",
            CodCliente = payment.CodCliente,
            CorreoDestino = email,
            FechaEmisionUtc = DateTime.UtcNow,
            EstadoEnvio = "NO_SOLICITADO",
            IntentosEnvio = 0
        };

        _dbContext.ComprobantesPago.Add(receipt);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.Entry(receipt).State = EntityState.Detached;
        }
    }

    public async Task<PaymentReceiptResult> GetByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadAsync(
            orderId,
            actorType,
            actorReferenceId,
            isAdministrator,
            cancellationToken);

        return load.Data is null
            ? new(load.Status, Detail: load.Detail)
            : new(PaymentReceiptStatus.Success, MapResponse(load.Data));
    }

    public async Task<PaymentReceiptResult> DownloadByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var load = await LoadAsync(
            orderId,
            actorType,
            actorReferenceId,
            isAdministrator,
            cancellationToken);

        if (load.Data is null)
        {
            return new(load.Status, Detail: load.Detail);
        }

        var pdf = _pdfGenerator.Generate(load.Data.Document);
        return new(
            PaymentReceiptStatus.Success,
            MapResponse(load.Data),
            new PaymentReceiptFile(
                pdf,
                $"{load.Data.Receipt.NumeroComprobante}.pdf"));
    }

    public async Task<PaymentReceiptResult> EmailByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var configurationError = _emailSender.ValidateConfiguration();
        if (configurationError is not null)
        {
            return new(
                PaymentReceiptStatus.EmailNotConfigured,
                Detail: configurationError);
        }

        var load = await LoadAsync(
            orderId,
            actorType,
            actorReferenceId,
            isAdministrator,
            cancellationToken);

        if (load.Data is null)
        {
            return new(load.Status, Detail: load.Detail);
        }

        var email = await GetRegisteredEmailAsync(
            load.Data.Payment.CodCliente,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            return new(
                PaymentReceiptStatus.RegisteredEmailMissing,
                MapResponse(load.Data),
                Detail: "El cliente no tiene un correo registrado.");
        }

        if (load.Data.Receipt.EstadoEnvio == "ENVIADO" &&
            string.Equals(
                load.Data.Receipt.CorreoDestino,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(
                PaymentReceiptStatus.Success,
                MapResponse(load.Data),
                Detail: "El comprobante ya fue enviado al correo registrado.");
        }

        var trackedReceipt = await _dbContext.ComprobantesPago.SingleAsync(
            item => item.IdComprobante == load.Data.Receipt.IdComprobante,
            cancellationToken);
        trackedReceipt.CorreoDestino = email;
        trackedReceipt.EstadoEnvio = "PENDIENTE";
        trackedReceipt.IntentosEnvio++;
        trackedReceipt.DetalleFallo = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var pdf = _pdfGenerator.Generate(load.Data.Document);
            await _emailSender.SendAsync(
                email,
                load.Data.Document.ClientName,
                trackedReceipt.NumeroComprobante,
                load.Data.Document.OrderCode,
                pdf,
                cancellationToken);

            trackedReceipt.EstadoEnvio = "ENVIADO";
            trackedReceipt.UltimoEnvioUtc = DateTime.UtcNow;
            trackedReceipt.DetalleFallo = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var refreshed = await LoadAsync(
                orderId,
                actorType,
                actorReferenceId,
                isAdministrator,
                cancellationToken);
            return new(
                PaymentReceiptStatus.Success,
                refreshed.Data is null ? null : MapResponse(refreshed.Data),
                Detail: "Comprobante enviado al correo registrado.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            trackedReceipt.EstadoEnvio = "FALLIDO";
            trackedReceipt.DetalleFallo = CleanFailure(exception.Message);
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            return new(
                PaymentReceiptStatus.EmailDeliveryFailed,
                MapResponse(load.Data) with
                {
                    RegisteredEmail = email,
                    EmailStatus = trackedReceipt.EstadoEnvio,
                    EmailAttempts = trackedReceipt.IntentosEnvio
                },
                Detail: "El pago permanece completado, pero no fue posible enviar el correo.");
        }
    }

    private async Task<ReceiptLoadResult> LoadAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Pedidos.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdPedido == orderId, cancellationToken);
        if (order is null)
        {
            return new(
                PaymentReceiptStatus.NotFound,
                null,
                $"No existe el pedido {orderId}.");
        }

        if (!isAdministrator &&
            !(actorType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
              order.CodCliente.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase)))
        {
            return new(
                PaymentReceiptStatus.Forbidden,
                null,
                "La cuenta no puede consultar el comprobante de este pedido.");
        }

        var payment = await _dbContext.PagosPedidos.AsNoTracking()
            .Where(item => item.IdPedido == orderId && item.Estado == "COMPLETED")
            .OrderByDescending(item => item.CompletadoElUtc)
            .ThenByDescending(item => item.IdPago)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            return new(
                PaymentReceiptStatus.PaymentNotCompleted,
                null,
                "El pedido todavía no tiene un pago completado.");
        }

        await EnsureForPaymentAsync(payment.IdPago, cancellationToken);

        var receipt = await _dbContext.ComprobantesPago.AsNoTracking()
            .SingleAsync(item => item.IdPago == payment.IdPago, cancellationToken);
        var client = await _dbContext.Clientes.AsNoTracking()
            .SingleAsync(item => item.CodCliente == order.CodCliente, cancellationToken);
        var branch = await _dbContext.Sucursales.AsNoTracking()
            .SingleAsync(item => item.CodSucursal == order.CodSucursal, cancellationToken);
        var company = await _dbContext.Empresas.AsNoTracking()
            .SingleAsync(item => item.CodEmpresa == branch.CodEmpresa, cancellationToken);
        var items = await
        (
            from detail in _dbContext.PedidosDetalle.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdPedido == order.IdPedido
            orderby detail.IdPedidoDetalle
            select new PaymentReceiptLine(
                detail.CodProducto,
                product.NombreProducto,
                detail.Cantidad,
                detail.PrecioUnitario,
                detail.Subtotal)
        ).ToArrayAsync(cancellationToken);
        var refundedAmount = await _dbContext.ReembolsosPedidos.AsNoTracking()
            .Where(item => item.IdPago == payment.IdPago && item.Estado == "COMPLETED")
            .SumAsync(item => (decimal?)item.MontoLocal, cancellationToken) ?? 0m;
        var registeredEmail = await GetRegisteredEmailAsync(
            payment.CodCliente,
            cancellationToken);

        var clientName = $"{client.NombreCliente} {client.ApellidoCliente}".Trim();
        var document = new PaymentReceiptDocumentData(
            receipt.IdComprobante,
            receipt.NumeroComprobante,
            payment.IdPago,
            order.IdPedido,
            order.CodPedido,
            client.CodCliente,
            clientName,
            client.CuiNit,
            branch.NombreSucursal,
            branch.DireccionSucursal,
            company.NombreEmpresa,
            payment.CodTipoPago,
            payment.ProveedorPago,
            payment.CapturaProveedorId ?? payment.ReferenciaExterna,
            order.Subtotal,
            order.CostoEnvio,
            order.DescuentoAplicado ?? 0m,
            payment.MontoLocal,
            payment.MonedaLocal,
            refundedAmount,
            payment.CompletadoElUtc ?? payment.CreadoElUtc,
            receipt.FechaEmisionUtc,
            items);

        return new(
            PaymentReceiptStatus.Success,
            new ReceiptData(receipt, payment, registeredEmail, document),
            null);
    }

    private async Task<string?> GetRegisteredEmailAsync(
        string clientCode,
        CancellationToken cancellationToken)
    {
        var clientEmail = await _dbContext.Clientes.AsNoTracking()
            .Where(item => item.CodCliente == clientCode)
            .Select(item => item.EmailCliente)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(clientEmail))
        {
            return clientEmail.Trim();
        }

        var accountEmail = await _dbContext.CuentasAcceso.AsNoTracking()
            .Where(item => item.ReferenciaId == clientCode &&
                           item.TipoUsuario == "Cliente" &&
                           item.Estado)
            .Select(item => item.Correo)
            .SingleOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(accountEmail) ? null : accountEmail.Trim();
    }

    private static PaymentReceiptResponse MapResponse(ReceiptData data) =>
        new(
            data.Receipt.IdComprobante,
            data.Receipt.NumeroComprobante,
            data.Payment.IdPago,
            data.Document.OrderId,
            data.Document.OrderCode,
            data.Document.ClientCode,
            data.Document.ClientName,
            data.RegisteredEmail,
            data.Payment.CodTipoPago,
            data.Payment.ProveedorPago,
            data.Payment.MontoLocal,
            data.Payment.MonedaLocal,
            data.Document.PaidAtUtc,
            data.Receipt.FechaEmisionUtc,
            data.Receipt.EstadoEnvio,
            data.Receipt.IntentosEnvio,
            data.Receipt.UltimoEnvioUtc,
            DeliveryMethods);

    private static string CleanFailure(string value) =>
        value.Length <= 500 ? value : value[..500];

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private sealed record ReceiptData(
        ComprobantePago Receipt,
        PagoPedido Payment,
        string? RegisteredEmail,
        PaymentReceiptDocumentData Document);

    private sealed record ReceiptLoadResult(
        PaymentReceiptStatus Status,
        ReceiptData? Data,
        string? Detail);
}
