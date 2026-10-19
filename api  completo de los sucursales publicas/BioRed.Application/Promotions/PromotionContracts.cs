using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Promotions;

public sealed class GetManagedPromotionsRequest
{
    [StringLength(15)]
    public string? CompanyCode { get; init; }

    [StringLength(15)]
    public string? BranchCode { get; init; }

    public bool IncludeInactive { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class GetAvailablePromotionsRequest
{
    [Required, StringLength(15)]
    public string BranchCode { get; init; } = string.Empty;

    [Range(0d, 9_999_999_999_999_999d)]
    public decimal OrderSubtotal { get; init; }
}

public sealed class SavePromotionRequest
{
    [Required, StringLength(15)]
    public string PromotionCode { get; init; } = string.Empty;

    [Required, StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [StringLength(15)]
    public string? BranchCode { get; init; }

    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; init; }

    [Required, RegularExpression("^(PERCENTAGE|FIXED)$")]
    public string DiscountType { get; init; } = string.Empty;

    [Range(0.01d, 9_999_999_999_999_999d)]
    public decimal DiscountValue { get; init; }

    [Range(0d, 9_999_999_999_999_999d)]
    public decimal MinimumOrderAmount { get; init; }

    [Range(0d, 9_999_999_999_999_999d)]
    public decimal? MaximumDiscountAmount { get; init; }

    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }

    [Range(1, int.MaxValue)]
    public int? TotalUsageLimit { get; init; }

    [Range(1, 1000)]
    public int PerClientUsageLimit { get; init; } = 1;

    [MaxLength(100)]
    public IReadOnlyCollection<string> ProductCodes { get; init; } =
        Array.Empty<string>();
}

public sealed class UpdatePromotionRequest
{
    [Required, StringLength(15)]
    public string CompanyCode { get; init; } = string.Empty;

    [StringLength(15)]
    public string? BranchCode { get; init; }

    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; init; }

    [Required, RegularExpression("^(PERCENTAGE|FIXED)$")]
    public string DiscountType { get; init; } = string.Empty;

    [Range(0.01d, 9_999_999_999_999_999d)]
    public decimal DiscountValue { get; init; }

    [Range(0d, 9_999_999_999_999_999d)]
    public decimal MinimumOrderAmount { get; init; }

    [Range(0d, 9_999_999_999_999_999d)]
    public decimal? MaximumDiscountAmount { get; init; }

    public DateTime StartsAtUtc { get; init; }
    public DateTime EndsAtUtc { get; init; }

    [Range(1, int.MaxValue)]
    public int? TotalUsageLimit { get; init; }

    [Range(1, 1000)]
    public int PerClientUsageLimit { get; init; } = 1;

    public bool Active { get; init; }

    [MaxLength(100)]
    public IReadOnlyCollection<string> ProductCodes { get; init; } =
        Array.Empty<string>();
}

public sealed record PromotionResponse(
    string PromotionCode,
    string CompanyCode,
    string? BranchCode,
    string Name,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal MinimumOrderAmount,
    decimal? MaximumDiscountAmount,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int? TotalUsageLimit,
    int PerClientUsageLimit,
    int UsedCount,
    bool Active,
    IReadOnlyCollection<string> ProductCodes);

public sealed record GetPromotionsResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<PromotionResponse> Items);

public enum PromotionManagementStatus
{
    Success,
    NotFound,
    CompanyNotFound,
    BranchNotFound,
    ProductNotFound,
    Duplicate,
    InvalidConfiguration,
    AlreadyUsed
}

public sealed record PromotionManagementResult(
    PromotionManagementStatus Status,
    PromotionResponse? Promotion = null,
    string? Detail = null);

public interface IPromotionManagementService
{
    Task<GetPromotionsResponse> GetAsync(
        GetManagedPromotionsRequest request,
        CancellationToken cancellationToken = default);

    Task<GetPromotionsResponse> GetAvailableAsync(
        string clientCode,
        GetAvailablePromotionsRequest request,
        CancellationToken cancellationToken = default);

    Task<PromotionManagementResult> GetByCodeAsync(
        string promotionCode,
        CancellationToken cancellationToken = default);

    Task<PromotionManagementResult> CreateAsync(
        string actorReferenceId,
        SavePromotionRequest request,
        CancellationToken cancellationToken = default);

    Task<PromotionManagementResult> UpdateAsync(
        string promotionCode,
        string actorReferenceId,
        UpdatePromotionRequest request,
        CancellationToken cancellationToken = default);

    Task<PromotionManagementResult> DeactivateAsync(
        string promotionCode,
        string actorReferenceId,
        CancellationToken cancellationToken = default);
}
