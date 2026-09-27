using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Inventory;

public sealed class GetBranchInventoryRequest
{
    [StringLength(100)]
    public string? Search { get; init; }

    public bool OnlyLowStock { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record InventoryItemResponse(
    int InventoryId,
    string BranchCode,
    string ProductCode,
    string ProductName,
    int CurrentQuantity,
    int MinimumStock,
    int MaximumStock,
    bool LowStock,
    DateTime UpdatedAtUtc);

public sealed record GetBranchInventoryResponse(
    string BranchCode,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<InventoryItemResponse> Items);

public sealed record ProductLotResponse(
    int LotId,
    string ProductCode,
    string LotNumber,
    DateOnly ExpirationDate,
    int Quantity,
    bool Expired,
    int DaysUntilExpiration);

public sealed class RegisterInventoryMovementRequest
{
    [Required]
    [StringLength(15)]
    public string BranchCode { get; init; } = string.Empty;

    [Required]
    [StringLength(15)]
    public string ProductCode { get; init; } = string.Empty;

    [Required]
    [RegularExpression(
        "^(Entrada|Salida)$",
        ErrorMessage = "MovementType debe ser Entrada o Salida.")]
    public string MovementType { get; init; } = string.Empty;

    [Range(1, 1_000_000)]
    public int Quantity { get; init; }

    [Range(0, 1_000_000)]
    public int? MinimumStock { get; init; }

    [Range(0, 1_000_000)]
    public int? MaximumStock { get; init; }

    [StringLength(50)]
    public string? LotNumber { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    public int? ReferenceId { get; init; }

    [StringLength(200)]
    public string? Observation { get; init; }
}

public sealed record InventoryMovementResponse(
    int KardexId,
    string BranchCode,
    string ProductCode,
    string MovementType,
    int Quantity,
    int PreviousQuantity,
    int CurrentQuantity,
    string? LotNumber,
    DateTime RegisteredAtUtc);

public sealed class GetKardexRequest
{
    [StringLength(15)]
    public string? BranchCode { get; init; }

    [StringLength(15)]
    public string? ProductCode { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record KardexMovementResponse(
    int KardexId,
    string ProductCode,
    string ProductName,
    string BranchCode,
    string MovementType,
    int Quantity,
    DateTime RegisteredAtUtc,
    int? ReferenceId,
    string? Observation);

public sealed record GetKardexResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<KardexMovementResponse> Items);

public enum InventoryManagementStatus
{
    Success,
    BranchNotFound,
    ProductNotFound,
    InventoryNotFound,
    LotNotFound,
    InsufficientStock,
    InvalidStockLevels,
    InvalidLot
}

public sealed record InventoryMovementResult(
    InventoryManagementStatus Status,
    InventoryMovementResponse? Movement = null,
    string? Detail = null);

public interface IInventoryManagementService
{
    Task<GetBranchInventoryResponse?> GetInventoryAsync(
        string branchCode,
        GetBranchInventoryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProductLotResponse>?> GetLotsAsync(
        string productCode,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementResult> RegisterMovementAsync(
        RegisterInventoryMovementRequest request,
        CancellationToken cancellationToken = default);

    Task<GetKardexResponse> GetKardexAsync(
        GetKardexRequest request,
        CancellationToken cancellationToken = default);
}
