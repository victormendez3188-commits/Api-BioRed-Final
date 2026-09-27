using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Products;

public sealed class GetManagedProductsRequest
{
    [StringLength(15)]
    public string? CompanyCode { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }

    public bool IncludeInactive { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record ManagedProductResponse(
    string ProductCode,
    string CompanyCode,
    string ProductName,
    string? Description,
    decimal CostPrice,
    decimal SalePrice,
    bool RequiresPrescription,
    bool Active,
    DateTime CreatedAtUtc,
    string CreatedBy);

public sealed record GetManagedProductsResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedProductResponse> Items);

public sealed class CreateManagedProductRequest
{
    [Required]
    [StringLength(15)]
    public string ProductCode { get; init; } = string.Empty;

    [Required]
    [StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ProductName { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; init; }

    [Range(0d, double.MaxValue)]
    public decimal CostPrice { get; init; }

    [Range(0.01d, double.MaxValue)]
    public decimal SalePrice { get; init; }

    public bool RequiresPrescription { get; init; }
}

public sealed class UpdateManagedProductRequest
{
    [Required]
    [StringLength(100)]
    public string ProductName { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; init; }

    [Range(0d, double.MaxValue)]
    public decimal CostPrice { get; init; }

    [Range(0.01d, double.MaxValue)]
    public decimal SalePrice { get; init; }

    public bool? RequiresPrescription { get; init; }

    public bool? Active { get; init; }
}

public enum ProductManagementStatus
{
    Success,
    NotFound,
    CompanyNotFound,
    Duplicate,
    InvalidPrice
}

public sealed record ProductManagementResult(
    ProductManagementStatus Status,
    ManagedProductResponse? Product = null,
    string? Detail = null);

public interface IProductManagementService
{
    Task<GetManagedProductsResponse> GetAsync(
        GetManagedProductsRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductManagementResult> GetByCodeAsync(
        string productCode,
        CancellationToken cancellationToken = default);

    Task<ProductManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductManagementResult> UpdateAsync(
        string productCode,
        UpdateManagedProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductManagementResult> DeactivateAsync(
        string productCode,
        CancellationToken cancellationToken = default);
}
