namespace BioRed.Application.Messaging;

public interface IOrderMessageService
{
    Task<OrderMessageAccessResult> CanParticipateAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        CancellationToken cancellationToken = default);

    Task<GetOrderMessagesResult> GetAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        long afterMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SendOrderMessageResult> SendAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        SendOrderMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<MarkOrderMessagesReadResult> MarkReadAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        long lastMessageId,
        CancellationToken cancellationToken = default);
}
