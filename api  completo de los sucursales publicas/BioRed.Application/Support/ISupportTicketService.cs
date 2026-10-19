namespace BioRed.Application.Support;

public interface ISupportTicketService
{
    Task<SupportTicketResult> CreateAsync(
        int accountId,
        string actorType,
        string actorReferenceId,
        CreateSupportTicketRequest request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketsResult> GetAsync(
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        GetSupportTicketsRequest request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketResult> GetByIdAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        CancellationToken cancellationToken = default);

    Task<SupportTicketMessagesResult> GetMessagesAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        long afterMessageId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SupportTicketMessageResult> SendMessageAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        SendSupportTicketMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<SupportTicketResult> UpdateAsync(
        long ticketId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool canManageAllTickets,
        UpdateSupportTicketRequest request,
        CancellationToken cancellationToken = default);
}
