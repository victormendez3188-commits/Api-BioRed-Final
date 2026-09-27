using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Accounting;

public sealed class GetManagedInvoicesRequest
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

public sealed class CreateManagedInvoiceRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; init; }

    [StringLength(30)]
    public string? DocumentSeries { get; init; }

    [Required, StringLength(50)]
    public string DocumentNumber { get; init; } = string.Empty;

    public DateOnly? DueDate { get; init; }

    [StringLength(300)]
    public string? Observations { get; init; }
}

public sealed record ManagedInvoiceItemResponse(
    int InvoiceDetailId,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal CostPrice,
    decimal SalePrice,
    decimal LineTotal);

public sealed record ManagedInvoiceSummaryResponse(
    int InvoiceId,
    int OrderId,
    string Document,
    string ClientCode,
    string ClientName,
    string BranchCode,
    DateTime InvoiceDateUtc,
    decimal Total,
    int StatusId,
    string Status,
    decimal ReceivableBalance);

public sealed record GetManagedInvoicesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedInvoiceSummaryResponse> Items);

public sealed record ManagedInvoiceResponse(
    int InvoiceId,
    int OrderId,
    string OrderCode,
    string DocumentSeries,
    string DocumentNumber,
    string ClientCode,
    string ClientName,
    string BranchCode,
    string BranchName,
    string UserCode,
    string PaymentTypeCode,
    string PaymentTypeName,
    DateTime InvoiceDateUtc,
    decimal Subtotal,
    decimal DeliveryCost,
    decimal Discount,
    decimal Total,
    int StatusId,
    string Status,
    string? Observations,
    int ReceivableId,
    decimal ReceivableBalance,
    DateOnly DueDate,
    IReadOnlyCollection<ManagedInvoiceItemResponse> Items);

public enum BillingManagementStatus
{
    Success,
    NotFound,
    OrderNotFound,
    OrderCancelled,
    OrderNotDelivered,
    UserNotFound,
    StatusNotConfigured,
    DuplicateOrder,
    DuplicateDocument,
    InvalidDueDate,
    InvalidTotal
}

public sealed record BillingManagementResult(
    BillingManagementStatus Status,
    ManagedInvoiceResponse? Invoice = null,
    string? Detail = null);

public interface IBillingManagementService
{
    Task<GetManagedInvoicesResponse> GetAsync(
        GetManagedInvoicesRequest request,
        CancellationToken cancellationToken = default);

    Task<BillingManagementResult> GetByIdAsync(
        int invoiceId,
        CancellationToken cancellationToken = default);

    Task<BillingManagementResult> CreateFromOrderAsync(
        string actorReferenceId,
        CreateManagedInvoiceRequest request,
        CancellationToken cancellationToken = default);
}
