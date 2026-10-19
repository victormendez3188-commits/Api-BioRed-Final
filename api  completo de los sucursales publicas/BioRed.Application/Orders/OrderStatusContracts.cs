using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Orders;

public sealed class UpdateOrderStatusRequest
{
    [Required]
    [RegularExpression(
        "^(Aceptado|En Preparación|En Camino|Entregado)$",
        ErrorMessage = "El estado enviado no es válido.")]
    public string Status { get; init; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; init; }
}

public sealed record UpdateOrderStatusResponse(
    int OrderId,
    string OrderCode,
    string PreviousStatus,
    string Status,
    string? DriverCode,
    DateTime UpdatedAtUtc,
    DateTime? DeliveredAtUtc);

public enum UpdateOrderStatusStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidTransition
}

public sealed record UpdateOrderStatusResult(
    UpdateOrderStatusStatus Status,
    UpdateOrderStatusResponse? Order = null,
    string? Detail = null);
