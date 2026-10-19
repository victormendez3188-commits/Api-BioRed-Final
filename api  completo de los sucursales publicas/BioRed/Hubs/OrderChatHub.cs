using System.Security.Claims;
using BioRed.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BioRed.Hubs;

/// <summary>
/// Real-time transport for the order chat. Messages are still created through
/// the REST endpoint, which keeps validation, persistence, auditing and push
/// notifications in one canonical flow.
/// </summary>
[Authorize]
public sealed class OrderChatHub : Hub
{
    private readonly IOrderMessageService _messageService;

    public OrderChatHub(IOrderMessageService messageService)
    {
        _messageService = messageService;
    }

    public async Task JoinOrder(int orderId)
    {
        if (!TryGetAccountId(out var accountId))
        {
            throw new HubException("UNAUTHORIZED");
        }

        var access = await _messageService.CanParticipateAsync(
            orderId,
            accountId,
            Context.User?.FindFirstValue("user_type") ?? string.Empty,
            Context.User?.FindFirstValue("reference_id") ?? string.Empty,
            Context.ConnectionAborted);

        if (access.Status != OrderMessageStatus.Success)
        {
            throw new HubException(access.Status switch
            {
                OrderMessageStatus.NotFound => "ORDER_NOT_FOUND",
                OrderMessageStatus.Forbidden => "FORBIDDEN",
                _ => "CHAT_NOT_AVAILABLE"
            });
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetOrderGroupName(orderId),
            Context.ConnectionAborted);
    }

    public Task LeaveOrder(int orderId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetOrderGroupName(orderId),
            Context.ConnectionAborted);

    public static string GetOrderGroupName(int orderId) => $"order-chat:{orderId}";

    private bool TryGetAccountId(out int accountId) =>
        int.TryParse(
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
            out accountId) && accountId > 0;
}
