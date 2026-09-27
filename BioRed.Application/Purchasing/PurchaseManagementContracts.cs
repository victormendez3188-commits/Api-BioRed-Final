using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Purchasing;

public sealed class GetManagedPurchasesRequest
{
    [StringLength(15)]
    public string? BranchCode { get; init; }

    [StringLength(15)]
    public string? SupplierCode { get; init; }

    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public bool IncludeCancelled { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record ManagedPurchaseSummaryResponse(
    int PurchaseId,
    string SupplierCode,
    string SupplierName,
    string BranchCode,
    string Document,
    DateTime PurchaseDateUtc,
    decimal Total,
    string PaymentTypeCode,
    string PaymentTypeName,
    int StatusId,
    string Status);

public sealed record GetManagedPurchasesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedPurchaseSummaryResponse> Items);

public sealed record ManagedPurchaseItemResponse(
    int PurchaseDetailId,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal CostPrice,
    decimal SalePrice,
    decimal LineTotal,
    string? LotNumber,
    DateOnly? ExpirationDate);

public sealed record ManagedPurchaseResponse(
    int PurchaseId,
    string SupplierCode,
    string SupplierName,
    string CompanyCode,
    string BranchCode,
    string BranchName,
    string UserCode,
    string DocumentSeries,
    string DocumentNumber,
    DateTime PurchaseDateUtc,
    decimal Total,
    string PaymentTypeCode,
    string PaymentTypeName,
    int StatusId,
    string Status,
    string? Observations,
    IReadOnlyCollection<ManagedPurchaseItemResponse> Items);

public sealed class CreateManagedPurchaseItemRequest
{
    [Required, StringLength(15)]
    public string ProductCode { get; init; } = string.Empty;

    [Range(1, 1_000_000)]
    public int Quantity { get; init; }

    [Range(0.01d, 9_999_999_999_999_999d)]
    public decimal CostPrice { get; init; }

    [Range(0.01d, 9_999_999_999_999_999d)]
    public decimal SalePrice { get; init; }

    [StringLength(50)]
    public string? LotNumber { get; init; }

    public DateOnly? ExpirationDate { get; init; }
}

public sealed class CreateManagedPurchaseRequest
{
    [Required, StringLength(15)]
    public string SupplierCode { get; init; } = string.Empty;

    [Required, StringLength(15)]
    public string BranchCode { get; init; } = string.Empty;

    [Required, StringLength(15)]
    public string PaymentTypeCode { get; init; } = string.Empty;

    [StringLength(30)]
    public string? DocumentSeries { get; init; }

    [Required, StringLength(50)]
    public string DocumentNumber { get; init; } = string.Empty;

    public DateTime? PurchaseDateUtc { get; init; }

    [StringLength(300)]
    public string? Observations { get; init; }

    [Required, MinLength(1)]
    public IReadOnlyCollection<CreateManagedPurchaseItemRequest> Items { get; init; } =
        Array.Empty<CreateManagedPurchaseItemRequest>();
}

public sealed class CancelManagedPurchaseRequest
{
    [Required, StringLength(200, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}

public enum PurchaseManagementStatus
{
    Success,
    NotFound,
    SupplierNotFound,
    SupplierCompanyMismatch,
    BranchNotFound,
    UserNotFound,
    PaymentTypeNotFound,
    StatusNotConfigured,
    ProductNotFound,
    ProductCompanyMismatch,
    DuplicateDocument,
    DuplicateProduct,
    InvalidDateRange,
    InvalidPurchaseDate,
    InvalidPrice,
    InvalidTotal,
    InvalidLot,
    InventoryLimitExceeded,
    InsufficientStockToCancel,
    AlreadyCancelled
}

public sealed record PurchaseManagementResult(
    PurchaseManagementStatus Status,
    ManagedPurchaseResponse? Purchase = null,
    string? Detail = null);

public interface IPurchaseManagementService
{
    Task<GetManagedPurchasesResponse> GetAsync(
        GetManagedPurchasesRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseManagementResult> GetByIdAsync(
        int purchaseId,
        CancellationToken cancellationToken = default);

    Task<PurchaseManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedPurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseManagementResult> CancelAsync(
        int purchaseId,
        string actorReferenceId,
        CancelManagedPurchaseRequest request,
        CancellationToken cancellationToken = default);
}
