using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Accounting;

public sealed class GetManagedReturnsRequest
{
    [StringLength(15)]
    public string? BranchCode { get; init; }

    [StringLength(15)]
    public string? ClientCode { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class CreateManagedReturnItemRequest
{
    [Required, StringLength(15)]
    public string ProductCode { get; init; } = string.Empty;

    [Range(1, 1_000_000)]
    public int Quantity { get; init; }

    [StringLength(50)]
    public string? LotNumber { get; init; }
}

public sealed class CreateManagedReturnRequest
{
    [Range(1, int.MaxValue)]
    public int InvoiceId { get; init; }

    [Required, StringLength(300, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;

    [Required, MinLength(1), MaxLength(100)]
    public IReadOnlyCollection<CreateManagedReturnItemRequest> Items { get; init; } =
        Array.Empty<CreateManagedReturnItemRequest>();
}

public sealed record ManagedReturnItemResponse(
    int ReturnDetailId,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal CostPrice,
    decimal SalePrice,
    decimal LineTotal,
    string? LotNumber);

public sealed record ManagedReturnSummaryResponse(
    int ReturnId,
    int InvoiceId,
    string ReturnNumber,
    string ClientCode,
    string BranchCode,
    DateTime ReturnDateUtc,
    decimal Total,
    decimal CreditApplied,
    decimal RefundPending,
    string Status);

public sealed record GetManagedReturnsResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedReturnSummaryResponse> Items);

public sealed record ManagedReturnResponse(
    int ReturnId,
    int InvoiceId,
    string ReturnNumber,
    string ClientCode,
    string ClientName,
    string BranchCode,
    string UserCode,
    string PaymentTypeCode,
    DateTime ReturnDateUtc,
    decimal Total,
    decimal CreditApplied,
    decimal RefundPending,
    int StatusId,
    string Status,
    string Reason,
    IReadOnlyCollection<ManagedReturnItemResponse> Items);

public enum SalesReturnManagementStatus
{
    Success,
    NotFound,
    InvoiceNotFound,
    OrderNotDelivered,
    UserNotFound,
    StatusNotConfigured,
    DuplicateProduct,
    ProductNotInInvoice,
    QuantityExceeded,
    InventoryLimitExceeded,
    LotNotFound,
    InvalidTotal
}

public sealed record SalesReturnManagementResult(
    SalesReturnManagementStatus Status,
    ManagedReturnResponse? Return = null,
    string? Detail = null);

public interface ISalesReturnManagementService
{
    Task<GetManagedReturnsResponse> GetAsync(
        GetManagedReturnsRequest request,
        CancellationToken cancellationToken = default);

    Task<SalesReturnManagementResult> GetByIdAsync(
        int returnId,
        CancellationToken cancellationToken = default);

    Task<SalesReturnManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedReturnRequest request,
        CancellationToken cancellationToken = default);
}
