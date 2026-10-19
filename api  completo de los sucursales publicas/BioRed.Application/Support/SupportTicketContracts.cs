using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Support;

public sealed class CreateSupportTicketRequest
{
    [Required]
    [StringLength(150, MinimumLength = 3)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string Category { get; init; } = "OTRO";

    [Range(1, int.MaxValue)]
    public int? OrderId { get; init; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed class GetSupportTicketsRequest
{
    [StringLength(20, MinimumLength = 1)]
    public string? Status { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class SendSupportTicketMessageRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed class UpdateSupportTicketRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Status { get; init; } = string.Empty;

    [StringLength(20, MinimumLength = 1)]
    public string? Priority { get; init; }
}

public sealed record SupportTicketResponse(
    long TicketId,
    string TicketNumber,
    int ClientAccountId,
    string? ClientCode,
    int? OrderId,
    string? OrderCode,
    string Subject,
    string Category,
    string Status,
    string Priority,
    int MessageCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? ClosedAtUtc);

public sealed record SupportTicketsResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<SupportTicketResponse> Items);

public sealed record SupportTicketMessageResponse(
    long MessageId,
    long TicketId,
    string Content,
    DateTime SentAtUtc,
    bool IsMine);

public sealed record SupportTicketMessagesResponse(
    long TicketId,
    long AfterMessageId,
    int PageSize,
    bool HasMore,
    IReadOnlyCollection<SupportTicketMessageResponse> Items);

public enum SupportTicketOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidRequest,
    InvalidState
}

public sealed record SupportTicketResult(
    SupportTicketOperationStatus Status,
    SupportTicketResponse? Ticket = null,
    string? Detail = null);

public sealed record SupportTicketsResult(
    SupportTicketOperationStatus Status,
    SupportTicketsResponse? Tickets = null,
    string? Detail = null);

public sealed record SupportTicketMessageResult(
    SupportTicketOperationStatus Status,
    SupportTicketMessageResponse? Message = null,
    string? Detail = null);

public sealed record SupportTicketMessagesResult(
    SupportTicketOperationStatus Status,
    SupportTicketMessagesResponse? Messages = null,
    string? Detail = null);
