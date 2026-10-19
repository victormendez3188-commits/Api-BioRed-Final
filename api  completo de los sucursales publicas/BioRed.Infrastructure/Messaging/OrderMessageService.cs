using System.Data;
using BioRed.Application.Messaging;
using BioRed.Application.Notifications;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BioRed.Infrastructure.Messaging;

public sealed class OrderMessageService : IOrderMessageService
{
    private readonly BioRedDbContext _dbContext;
    private readonly IPushNotificationSender _pushSender;
    private readonly IOrderChatRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<OrderMessageService> _logger;

    public OrderMessageService(
        BioRedDbContext dbContext,
        IPushNotificationSender pushSender,
        IOrderChatRealtimeNotifier realtimeNotifier,
        ILogger<OrderMessageService> logger)
    {
        _dbContext = dbContext;
        _pushSender = pushSender;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<OrderMessageAccessResult> CanParticipateAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        CancellationToken cancellationToken = default)
    {
        var participant = await ResolveParticipantAsync(
            orderId,
            accountId,
            actorType,
            actorReferenceId,
            cancellationToken);

        return new OrderMessageAccessResult(
            participant.Status,
            participant.Detail);
    }

    public async Task<GetOrderMessagesResult> GetAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        long afterMessageId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (afterMessageId < 0 || pageSize is < 1 or > 100)
        {
            return new(
                OrderMessageStatus.InvalidRequest,
                Detail: "afterMessageId debe ser mayor o igual a cero y pageSize debe estar entre 1 y 100.");
        }

        var participant = await ResolveParticipantAsync(
            orderId,
            accountId,
            actorType,
            actorReferenceId,
            cancellationToken);

        if (participant.Status != OrderMessageStatus.Success || participant.Context is null)
        {
            return new(participant.Status, Detail: participant.Detail);
        }

        var rows = await _dbContext.MensajesPedido
            .AsNoTracking()
            .Where(item =>
                item.IdPedido == orderId &&
                item.IdMensaje > afterMessageId &&
                !item.Eliminado)
            .OrderBy(item => item.IdMensaje)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var items = rows
            .Take(pageSize)
            .Select(item => Map(item, accountId))
            .ToArray();

        return new(
            OrderMessageStatus.Success,
            new GetOrderMessagesResponse(
                orderId,
                afterMessageId,
                pageSize,
                hasMore,
                items));
    }

    public async Task<SendOrderMessageResult> SendAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        SendOrderMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = request.Content.Trim();

        if (content.Length is < 1 or > 1000)
        {
            return new(
                OrderMessageStatus.InvalidRequest,
                Detail: "El mensaje debe tener entre 1 y 1000 caracteres.");
        }

        var participant = await ResolveParticipantAsync(
            orderId,
            accountId,
            actorType,
            actorReferenceId,
            cancellationToken);

        if (participant.Status != OrderMessageStatus.Success || participant.Context is null)
        {
            return new(participant.Status, Detail: participant.Detail);
        }

        if (participant.Context.Order.EstadoPedido is not
                ("Aceptado" or "En Preparación" or "En Camino"))
        {
            return new(
                OrderMessageStatus.InvalidState,
                Detail: "No se pueden enviar mensajes cuando el pedido está Entregado o Cancelado.");
        }

        var message = new MensajePedido
        {
            IdPedido = orderId,
            IdCuentaRemitente = accountId,
            IdCuentaDestinatario = participant.Context.RecipientAccountId,
            TipoMensaje = "Texto",
            Contenido = content,
            FechaEnvioUtc = DateTime.UtcNow,
            Leido = false,
            Eliminado = false
        };

        _dbContext.MensajesPedido.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = Map(message, accountId);
        await _pushSender.SendToAccountAsync(
            participant.Context.RecipientAccountId,
            "order_message",
            $"Mensaje del pedido {participant.Context.Order.CodPedido}",
            content,
            new Dictionary<string, string>
            {
                ["orderId"] = orderId.ToString(),
                ["messageId"] = message.IdMensaje.ToString(),
                ["orderCode"] = participant.Context.Order.CodPedido
            },
            cancellationToken);

        try
        {
            await _realtimeNotifier.MessageCreatedAsync(
                new OrderChatMessageRealtimeEvent(
                    message.IdPedido,
                    message.IdMensaje,
                    message.IdCuentaRemitente,
                    message.IdCuentaDestinatario,
                    message.TipoMensaje,
                    message.Contenido,
                    message.FechaEnvioUtc),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // SignalR is optional: the message was already persisted and the
            // receiver can retrieve it through the REST history endpoint.
            _logger.LogWarning(
                exception,
                "No fue posible publicar en tiempo real el mensaje {MessageId} del pedido {OrderId}.",
                message.IdMensaje,
                message.IdPedido);
        }

        return new(OrderMessageStatus.Success, response);
    }

    public async Task<MarkOrderMessagesReadResult> MarkReadAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        long lastMessageId,
        CancellationToken cancellationToken = default)
    {
        if (lastMessageId <= 0)
        {
            return new(
                OrderMessageStatus.InvalidRequest,
                Detail: "lastMessageId debe ser mayor que cero.");
        }

        var participant = await ResolveParticipantAsync(
            orderId,
            accountId,
            actorType,
            actorReferenceId,
            cancellationToken);

        if (participant.Status != OrderMessageStatus.Success || participant.Context is null)
        {
            return new(participant.Status, Detail: participant.Detail);
        }

        var messages = await _dbContext.MensajesPedido
            .Where(item =>
                item.IdPedido == orderId &&
                item.IdCuentaDestinatario == accountId &&
                item.IdMensaje <= lastMessageId &&
                !item.Leido &&
                !item.Eliminado)
            .ToListAsync(cancellationToken);

        var nowUtc = DateTime.UtcNow;
        foreach (var message in messages)
        {
            message.Leido = true;
            message.FechaLecturaUtc = nowUtc;
        }

        if (messages.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                await _realtimeNotifier.MessagesReadAsync(
                    new OrderChatMessagesReadRealtimeEvent(
                        orderId,
                        accountId,
                        lastMessageId,
                        messages.Count,
                        nowUtc),
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "No fue posible publicar en tiempo real la lectura de mensajes del pedido {OrderId}.",
                    orderId);
            }
        }

        return new(OrderMessageStatus.Success, messages.Count);
    }

    private async Task<ParticipantResult> ResolveParticipantAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdPedido == orderId, cancellationToken);

        if (order is null)
        {
            return new(OrderMessageStatus.NotFound, null, "No se encontró el pedido.");
        }

        var isClient = actorType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
            order.CodCliente.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase);
        var isDriver = actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(order.CodRepartidor) &&
            order.CodRepartidor.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase);

        if (!isClient && !isDriver)
        {
            return new(
                OrderMessageStatus.Forbidden,
                null,
                "Solo el cliente propietario y el repartidor asignado pueden participar en el chat.");
        }

        var account = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdCuenta == accountId &&
                        item.Estado &&
                        item.TipoUsuario == actorType &&
                        item.ReferenciaId == actorReferenceId,
                cancellationToken);

        if (account is null)
        {
            return new(
                OrderMessageStatus.Forbidden,
                null,
                "La cuenta autenticada no coincide con el participante del pedido.");
        }

        var recipientReference = isClient
            ? order.CodRepartidor
            : order.CodCliente;

        if (string.IsNullOrWhiteSpace(recipientReference))
        {
            return new(
                OrderMessageStatus.InvalidState,
                null,
                "El pedido todavía no tiene dos participantes para el chat.");
        }

        var recipientType = isClient ? "Repartidor" : "Cliente";
        var recipient = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Estado &&
                        item.TipoUsuario == recipientType &&
                        item.ReferenciaId == recipientReference,
                cancellationToken);

        if (recipient is null)
        {
            return new(
                OrderMessageStatus.InvalidState,
                null,
                "No se encontró la cuenta activa del otro participante.");
        }

        return new(
            OrderMessageStatus.Success,
            new ParticipantContext(order, recipient.IdCuenta),
            null);
    }

    private static OrderMessageResponse Map(
        MensajePedido item,
        int currentAccountId) =>
        new(
            item.IdMensaje,
            item.IdPedido,
            item.TipoMensaje,
            item.Contenido,
            item.FechaEnvioUtc,
            item.Leido,
            item.FechaLecturaUtc,
            item.IdCuentaRemitente == currentAccountId);

    private sealed record ParticipantContext(Pedido Order, int RecipientAccountId);

    private sealed record ParticipantResult(
        OrderMessageStatus Status,
        ParticipantContext? Context,
        string? Detail);
}
