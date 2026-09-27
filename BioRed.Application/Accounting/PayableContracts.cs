using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Accounting;

public sealed class GetPayablesRequest
{
    [StringLength(15)]
    public string? SupplierCode { get; init; }

    [StringLength(15)]
    public string? BranchCode { get; init; }

    public bool IncludePaid { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class CreatePayableFromPurchaseRequest
{
    [Range(1, int.MaxValue)]
    public int PurchaseId { get; init; }

    public DateOnly? DueDate { get; init; }
}

public sealed class RegisterPayablePaymentRequest
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

public sealed record PayableSummaryResponse(
    int PayableId,
    int PurchaseId,
    string SupplierCode,
    string SupplierName,
    string BranchCode,
    decimal OriginalAmount,
    decimal Balance,
    DateOnly DueDate,
    string Status);

public sealed record GetPayablesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<PayableSummaryResponse> Items);

public sealed record PayableResponse(
    int PayableId,
    int PurchaseId,
    string PurchaseDocument,
    string SupplierCode,
    string SupplierName,
    string BranchCode,
    DateTime CreatedAtUtc,
    DateOnly DueDate,
    decimal OriginalAmount,
    decimal Balance,
    int StatusId,
    string Status,
    IReadOnlyCollection<AccountMovementResponse> Movements);

public enum PayableManagementStatus
{
    Success,
    NotFound,
    PurchaseNotFound,
    PurchaseCancelled,
    DuplicatePurchase,
    UserNotFound,
    PaymentTypeNotFound,
    InvalidDueDate,
    InvalidAmount,
    AlreadyPaid,
    StatusNotConfigured
}

public sealed record PayableManagementResult(
    PayableManagementStatus Status,
    PayableResponse? Payable = null,
    string? Detail = null);

public interface IPayableManagementService
{
    Task<GetPayablesResponse> GetAsync(
        GetPayablesRequest request,
        CancellationToken cancellationToken = default);

    Task<PayableManagementResult> GetByIdAsync(
        int payableId,
        CancellationToken cancellationToken = default);

    Task<PayableManagementResult> CreateFromPurchaseAsync(
        string actorReferenceId,
        CreatePayableFromPurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<PayableManagementResult> RegisterPaymentAsync(
        int payableId,
        string actorReferenceId,
        RegisterPayablePaymentRequest request,
        CancellationToken cancellationToken = default);
}
