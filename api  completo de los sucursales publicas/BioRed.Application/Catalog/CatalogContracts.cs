using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Catalog;

public sealed class GetCatalogProductsRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public bool OnlyAvailable { get; init; } = true;

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record CatalogProductResponse(
    string ProductCode,
    string ProductName,
    string? Description,
    decimal SalePrice,
    int AvailableQuantity,
    bool InStock,
    bool RequiresPrescription,
    bool CanSellByUnit = false,
    decimal? UnitPrice = null,
    string? BaseUnit = null,
    int? UnitsPerPresentation = null,
    int? AvailableUnits = null,
    int? AvailablePresentations = null);

public sealed record GetCatalogProductsResponse(
    string BranchCode,
    string BranchName,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<CatalogProductResponse> Items);

public sealed record CatalogProductDetailResponse(
    string BranchCode,
    string BranchName,
    string ProductCode,
    string ProductName,
    string? Description,
    decimal SalePrice,
    int AvailableQuantity,
    int MinimumStock,
    int MaximumStock,
    bool InStock,
    DateTime InventoryUpdatedAtUtc,
    bool RequiresPrescription,
    bool CanSellByUnit = false,
    decimal? UnitPrice = null,
    string? BaseUnit = null,
    int? UnitsPerPresentation = null,
    int? AvailableUnits = null,
    int? AvailablePresentations = null);

public sealed record CatalogPaymentTypeResponse(
    string PaymentTypeCode,
    string PaymentTypeName);

public enum CatalogQueryStatus
{
    Success,
    BranchNotFound,
    ProductNotFound
}

public sealed record GetCatalogProductsResult(
    CatalogQueryStatus Status,
    GetCatalogProductsResponse? Catalog = null,
    string? Detail = null);

public sealed record GetCatalogProductDetailResult(
    CatalogQueryStatus Status,
    CatalogProductDetailResponse? Product = null,
    string? Detail = null);

public interface ICatalogService
{
    Task<IReadOnlyCollection<CatalogPaymentTypeResponse>> GetPaymentTypesAsync(
        CancellationToken cancellationToken = default);

    Task<GetCatalogProductsResult> GetProductsAsync(
        string branchCode,
        GetCatalogProductsRequest request,
        CancellationToken cancellationToken = default);

    Task<GetCatalogProductDetailResult> GetProductByCodeAsync(
        string branchCode,
        string productCode,
        CancellationToken cancellationToken = default);
}
