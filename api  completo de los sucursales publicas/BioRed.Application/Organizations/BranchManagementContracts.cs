using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Organizations;

public sealed class GetManagedBranchesRequest
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

public sealed record DeliveryConfigurationResponse(
    decimal MaximumRadiusKm,
    decimal BaseRate,
    decimal ExtraKilometerRate,
    int EstimatedMinutes,
    bool Active);

public sealed record ManagedBranchResponse(
    string BranchCode,
    string CompanyCode,
    string CompanyName,
    string BranchName,
    string Address,
    string Phone,
    string Email,
    decimal? Latitude,
    decimal? Longitude,
    bool Active,
    DateTime CreatedAtUtc,
    DeliveryConfigurationResponse? DeliveryConfiguration);

public sealed record GetManagedBranchesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<ManagedBranchResponse> Items);

public sealed class CreateManagedBranchRequest
{
    [Required]
    [StringLength(15)]
    public string BranchCode { get; init; } = string.Empty;

    [Required]
    [StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string BranchName { get; init; } = string.Empty;

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

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }
}

public sealed class UpdateManagedBranchRequest
{
    [Required]
    [StringLength(100)]
    public string BranchName { get; init; } = string.Empty;

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

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public bool? Active { get; init; }
}

public sealed class UpsertDeliveryConfigurationRequest
{
    [Range(0.01d, 100d)]
    public decimal MaximumRadiusKm { get; init; }

    [Range(0d, double.MaxValue)]
    public decimal BaseRate { get; init; }

    [Range(0d, double.MaxValue)]
    public decimal ExtraKilometerRate { get; init; }

    [Range(1, 1_440)]
    public int EstimatedMinutes { get; init; }

    public bool Active { get; init; } = true;
}

public enum BranchManagementStatus
{
    Success,
    NotFound,
    CompanyNotFound,
    DuplicateCode,
    DuplicateEmail,
    DuplicateData,
    InvalidCoordinates,
    InvalidDeliveryConfiguration
}

public sealed record BranchManagementResult(
    BranchManagementStatus Status,
    ManagedBranchResponse? Branch = null,
    string? Detail = null);

public interface IBranchManagementService
{
    Task<GetManagedBranchesResponse> GetAsync(
        GetManagedBranchesRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchManagementResult> GetByCodeAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task<BranchManagementResult> CreateAsync(
        CreateManagedBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchManagementResult> UpdateAsync(
        string branchCode,
        UpdateManagedBranchRequest request,
        CancellationToken cancellationToken = default);

    Task<BranchManagementResult> DeactivateAsync(
        string branchCode,
        CancellationToken cancellationToken = default);

    Task<BranchManagementResult> UpsertDeliveryConfigurationAsync(
        string branchCode,
        UpsertDeliveryConfigurationRequest request,
        CancellationToken cancellationToken = default);
}
