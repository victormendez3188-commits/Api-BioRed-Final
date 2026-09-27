using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Accounting;

public sealed class GetReceivablesRequest
{
    [StringLength(15)]
    public string? ClientCode { get; init; }

    [StringLength(15)]
    public string? BranchCode { get; init; }

    public bool IncludePaid { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class RegisterReceivablePaymentRequest
{
    [Range(0.01d, 9_999_999_999_999_999d)]
    public decimal Amount { get; init; }

    [Required, StringLength(15)]
    public string PaymentTypeCode { get; init; } = string.Empty;

    [StringLength(100)]
    public string? ExternalReference { get; init; }

    [StringLength(300)]
    public string? Observations { get; init; }
}

public sealed record AccountMovementResponse(
    int MovementId,
    string MovementType,
    decimal Amount,
    decimal PreviousBalance,
    decimal NewBalance,
    string UserCode,
    string? PaymentTypeCode,
    string? ExternalReference,
    string? Observations,
    DateTime MovementDateUtc);

public sealed record ReceivableSummaryResponse(
    int ReceivableId,
    int InvoiceId,
    string ClientCode,
    string ClientName,
    string BranchCode,
    decimal OriginalAmount,
    decimal Balance,
    DateOnly DueDate,
    string Status);

public sealed record GetReceivablesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ReceivableSummaryResponse> Items);

public sealed record ReceivableResponse(
    int ReceivableId,
    int InvoiceId,
    string InvoiceDocument,
    int OrderId,
    string ClientCode,
    string ClientName,
    string BranchCode,
    DateTime CreatedAtUtc,
    DateOnly DueDate,
    decimal OriginalAmount,
    decimal Balance,
    int StatusId,
    string Status,
    IReadOnlyCollection<AccountMovementResponse> Movements);

public enum ReceivableManagementStatus
{
    Success,
    NotFound,
    UserNotFound,
    PaymentTypeNotFound,
    CashRequiresDeliveredOrder,
    PayPalRequiresVerifiedCapture,
    InvalidAmount,
    AlreadyPaid,
    StatusNotConfigured
}

public sealed record ReceivableManagementResult(
    ReceivableManagementStatus Status,
    ReceivableResponse? Receivable = null,
    string? Detail = null);

public interface IReceivableManagementService
{
    Task<GetReceivablesResponse> GetAsync(
        GetReceivablesRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceivableManagementResult> GetByIdAsync(
        int receivableId,
        CancellationToken cancellationToken = default);

    Task<ReceivableManagementResult> RegisterPaymentAsync(
        int receivableId,
        string actorReferenceId,
        RegisterReceivablePaymentRequest request,
        CancellationToken cancellationToken = default);
}
