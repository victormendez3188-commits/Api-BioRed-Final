using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Purchasing;

public sealed class GetManagedSuppliersRequest
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

public sealed record ManagedSupplierResponse(
    string SupplierCode,
    string CompanyCode,
    string CompanyName,
    string SupplierName,
    string TaxId,
    string BusinessName,
    string Phone,
    string Email,
    bool Active,
    DateTime CreatedAtUtc,
    string CreatedBy);

public sealed record GetManagedSuppliersResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedSupplierResponse> Items);

public sealed class CreateManagedSupplierRequest
{
    [Required, StringLength(15)]
    public string SupplierCode { get; init; } = string.Empty;

    [Required, StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string SupplierName { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string TaxId { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string BusinessName { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; init; } = string.Empty;
}

public sealed class UpdateManagedSupplierRequest
{
    [Required, StringLength(100)]
    public string SupplierName { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string TaxId { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string BusinessName { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; init; } = string.Empty;

    public bool? Active { get; init; }
}

public enum SupplierManagementStatus
{
    Success,
    NotFound,
    CompanyNotFound,
    DuplicateCode,
    DuplicateTaxId,
    DuplicateEmail,
    DuplicateData
}

public sealed record SupplierManagementResult(
    SupplierManagementStatus Status,
    ManagedSupplierResponse? Supplier = null,
    string? Detail = null);

public interface ISupplierManagementService
{
    Task<GetManagedSuppliersResponse> GetAsync(
        GetManagedSuppliersRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierManagementResult> GetByCodeAsync(
        string supplierCode,
        CancellationToken cancellationToken = default);

    Task<SupplierManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierManagementResult> UpdateAsync(
        string supplierCode,
        UpdateManagedSupplierRequest request,
        CancellationToken cancellationToken = default);

    Task<SupplierManagementResult> DeactivateAsync(
        string supplierCode,
        CancellationToken cancellationToken = default);
}
