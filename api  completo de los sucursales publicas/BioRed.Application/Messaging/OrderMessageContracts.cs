using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Messaging;

public sealed class SendOrderMessageRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed class MarkOrderMessagesReadRequest
{
    [Range(1, long.MaxValue)]
    public long LastMessageId { get; init; }
}

public sealed record OrderMessageResponse(
    long MessageId,
    int OrderId,
    string Type,
    string Content,
    DateTime SentAtUtc,
    bool Read,
    DateTime? ReadAtUtc,
    bool IsMine);

public sealed record GetOrderMessagesResponse(
    int OrderId,
    long AfterMessageId,
    int PageSize,
    bool HasMore,
    IReadOnlyCollection<OrderMessageResponse> Items);

public enum OrderMessageStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidState,
    InvalidRequest
}

public sealed record GetOrderMessagesResult(
    OrderMessageStatus Status,
    GetOrderMessagesResponse? Messages = null,
    string? Detail = null);

public sealed record SendOrderMessageResult(
    OrderMessageStatus Status,
    OrderMessageResponse? Message = null,
    string? Detail = null);

public sealed record MarkOrderMessagesReadResult(
    OrderMessageStatus Status,
    int MarkedCount = 0,
    string? Detail = null);

public sealed record OrderMessageAccessResult(
    OrderMessageStatus Status,
    string? Detail = null);

