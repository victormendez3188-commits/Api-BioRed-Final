using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Reporting;

public abstract class ReportPeriodRequest : IValidatableObject
{
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }

    [StringLength(15)]
    public string? BranchCode { get; init; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var effectiveToUtc = ToUtc ?? DateTime.UtcNow;
        var effectiveFromUtc = FromUtc ?? effectiveToUtc.AddDays(-30);

        if (effectiveFromUtc > effectiveToUtc)
        {
            yield return new ValidationResult(
                "FromUtc no puede ser posterior a ToUtc.",
                new[] { nameof(FromUtc), nameof(ToUtc) });
        }

        if (effectiveToUtc - effectiveFromUtc > TimeSpan.FromDays(366))
        {
            yield return new ValidationResult(
                "El período máximo permitido es de 366 días.",
                new[] { nameof(FromUtc), nameof(ToUtc) });
        }
    }
}

public sealed class DashboardReportRequest : ReportPeriodRequest
{
}

public sealed class DailySalesReportRequest : ReportPeriodRequest
{
}

public sealed class LowStockReportRequest
{
    [StringLength(15)]
    public string? BranchCode { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class AuditReportRequest : ReportPeriodRequest
{
    [StringLength(50)]
    public string? Module { get; init; }

    [StringLength(50)]
    public string? ReferenceId { get; init; }

    [Range(100, 599)]
    public int? StatusCode { get; init; }

    [RegularExpression("^(GET|POST|PUT|PATCH|DELETE)$")]
    public string? HttpMethod { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record DashboardReportResponse(
    DateTime FromUtc,
    DateTime ToUtc,
    string? BranchCode,
    int TotalOrders,
    int DeliveredOrders,
    int CancelledOrders,
    int CompletedPayments,
    decimal GrossSales,
    decimal CompletedRefunds,
    decimal NetSales,
    int ActiveClients,
    int LowStockProducts,
    DateTime GeneratedAtUtc);

public sealed record DailySalesItemResponse(
    DateOnly Date,
    int Orders,
    int CompletedPayments,
    decimal GrossSales,
    decimal CompletedRefunds,
    decimal NetSales);

public sealed record DailySalesReportResponse(
    DateTime FromUtc,
    DateTime ToUtc,
    string? BranchCode,
    IReadOnlyCollection<DailySalesItemResponse> Items,
    DateTime GeneratedAtUtc);

public sealed record LowStockItemResponse(
    int InventoryId,
    string BranchCode,
    string BranchName,
    string ProductCode,
    string ProductName,
    int CurrentQuantity,
    int MinimumStock,
    int MissingToMinimum,
    DateTime UpdatedAtUtc);

public sealed record LowStockReportResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<LowStockItemResponse> Items);

public sealed record AuditItemResponse(
    long AuditId,
    int? AccountId,
    string? UserType,
    string? ReferenceId,
    string Module,
    string HttpMethod,
    string Path,
    int StatusCode,
    long DurationMs,
    string? IpAddress,
    string CorrelationId,
    DateTime RegisteredAtUtc);

public sealed record AuditReportResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<AuditItemResponse> Items);

public sealed record ApiAuditWriteRequest(
    int? AccountId,
    string? UserType,
    string? ReferenceId,
    string Module,
    string HttpMethod,
    string Path,
    int StatusCode,
    long DurationMs,
    string? IpAddress,
    string? UserAgent,
    string CorrelationId,
    DateTime RegisteredAtUtc);

public interface IReportingService
{
    Task<DashboardReportResponse> GetDashboardAsync(
        DashboardReportRequest request,
        CancellationToken cancellationToken = default);

    Task<DailySalesReportResponse> GetDailySalesAsync(
        DailySalesReportRequest request,
        CancellationToken cancellationToken = default);

    Task<LowStockReportResponse> GetLowStockAsync(
        LowStockReportRequest request,
        CancellationToken cancellationToken = default);

    Task<AuditReportResponse> GetAuditAsync(
        AuditReportRequest request,
        CancellationToken cancellationToken = default);
}

public interface IApiAuditWriter
{
    Task WriteAsync(
        ApiAuditWriteRequest request,
        CancellationToken cancellationToken = default);
}
