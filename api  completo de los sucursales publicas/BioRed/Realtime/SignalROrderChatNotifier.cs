using BioRed.Application.Messaging;
using BioRed.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BioRed.Realtime;

public sealed class SignalROrderChatNotifier : IOrderChatRealtimeNotifier
{
    private readonly IHubContext<OrderChatHub> _hubContext;
    private readonly ILogger<SignalROrderChatNotifier> _logger;

    public SignalROrderChatNotifier(
        IHubContext<OrderChatHub> hubContext,
        ILogger<SignalROrderChatNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task MessageCreatedAsync(
        OrderChatMessageRealtimeEvent message,
        CancellationToken cancellationToken = default) =>
        SendBestEffortAsync(
            "orderMessageCreated",
            message.OrderId,
            message,
            cancellationToken);

    public Task MessagesReadAsync(
        OrderChatMessagesReadRealtimeEvent notification,
        CancellationToken cancellationToken = default) =>
        SendBestEffortAsync(
            "orderMessagesRead",
            notification.OrderId,
            notification,
            cancellationToken);

    private async Task SendBestEffortAsync(
        string eventName,
        int orderId,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .Group(OrderChatHub.GetOrderGroupName(orderId))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // SignalR is an optimization; REST and SQL remain the source of truth.
            _logger.LogWarning(
                exception,
                "No fue posible publicar el evento de chat {EventName} del pedido {OrderId}.",
                eventName,
                orderId);
        }
    }
}
