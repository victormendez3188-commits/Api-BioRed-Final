using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Organizations;

public sealed class GetManagedCompaniesRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public bool IncludeInactive { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record ManagedCompanyResponse(
    string CompanyCode,
    string CompanyName,
    string Address,
    string Phone,
    string Email,
    bool Active,
    DateTime CreatedAtUtc);

public sealed record GetManagedCompaniesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedCompanyResponse> Items);

public sealed class CreateManagedCompanyRequest
{
    [Required]
    [StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string CompanyName { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Address { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;
}

public sealed class UpdateManagedCompanyRequest
{
    [Required]
    [StringLength(100)]
    public string CompanyName { get; init; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Address { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    public bool? Active { get; init; }
}

public enum CompanyManagementStatus
{
    Success,
    NotFound,
    DuplicateCode,
    DuplicateEmail,
    DuplicateData
}

public sealed record CompanyManagementResult(
    CompanyManagementStatus Status,
    ManagedCompanyResponse? Company = null,
    string? Detail = null);

public interface ICompanyManagementService
{
    Task<GetManagedCompaniesResponse> GetAsync(
        GetManagedCompaniesRequest request,
        CancellationToken cancellationToken = default);

    Task<CompanyManagementResult> GetByCodeAsync(
        string companyCode,
        CancellationToken cancellationToken = default);

    Task<CompanyManagementResult> CreateAsync(
        CreateManagedCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<CompanyManagementResult> UpdateAsync(
        string companyCode,
        UpdateManagedCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<CompanyManagementResult> DeactivateAsync(
        string companyCode,
        CancellationToken cancellationToken = default);
}
