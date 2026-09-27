using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Orders;

public sealed class AssignDriverRequest
{
    [Required]
    [StringLength(15)]
    public string DriverCode { get; init; } = string.Empty;
}

public sealed record AssignDriverResponse(
    int OrderId,
    string OrderCode,
    string DriverCode,
    string Status,
    DateTime AssignedAtUtc);

public enum AssignDriverStatus
{
    Success,
    OrderNotFound,
    DriverNotFound,
    DriverUnavailable,
    InvalidOrderState
}

public sealed record AssignDriverResult(
    AssignDriverStatus Status,
    AssignDriverResponse? Assignment = null,
    string? Detail = null);
