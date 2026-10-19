namespace BioRed.Application.Messaging;

/// <summary>
/// Publishes best-effort events after the canonical REST chat operation was
/// stored in SQL. Implementations must never make the chat persistence fail.
/// </summary>
public interface IOrderChatRealtimeNotifier
{
    Task MessageCreatedAsync(
        OrderChatMessageRealtimeEvent message,
        CancellationToken cancellationToken = default);

    Task MessagesReadAsync(
        OrderChatMessagesReadRealtimeEvent notification,
        CancellationToken cancellationToken = default);
}

public sealed record OrderChatMessageRealtimeEvent(
    int OrderId,
    long MessageId,
    int SenderAccountId,
    int RecipientAccountId,
    string Type,
    string Content,
    DateTime SentAtUtc);

public sealed record OrderChatMessagesReadRealtimeEvent(
    int OrderId,
    int ReaderAccountId,
    long LastMessageId,
    int MarkedCount,
    DateTime ReadAtUtc);
