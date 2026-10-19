using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Orders;

public sealed class CancelOrderRequest
{
    [StringLength(255)]
    public string? Reason { get; init; }
}

public sealed record CancelOrderResponse(
    int OrderId,
    string OrderCode,
    string PreviousStatus,
    string Status,
    string? DriverCode,
    string? Reason,
    DateTime CancelledAtUtc);

public enum CancelOrderStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidState,
    PaymentCompleted
}

public sealed record CancelOrderResult(
    CancelOrderStatus Status,
    CancelOrderResponse? Order = null,
    string? Detail = null);
