using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Delivery;

public sealed class VerifyDeliveryCodeRequest
{
    [Required]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "El código de entrega debe tener exactamente 4 dígitos.")]
    public string Code { get; init; } = string.Empty;
}

public sealed record DeliveryCodeDispatchResponse(
    int OrderId,
    string OrderCode,
    DateTime ExpiresAtUtc,
    string EmailStatus);

public sealed record DeliveryVerificationItemResponse(
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    bool SoldByUnit,
    string? BaseUnit);

public sealed record DeliveryVerificationResponse(
    int OrderId,
    string OrderCode,
    string ClientName,
    string PaymentTypeCode,
    bool IsPaid,
    decimal TotalCharged,
    decimal AmountToCollect,
    string Currency,
    IReadOnlyCollection<DeliveryVerificationItemResponse> Items,
    DateTime VerifiedAtUtc);

public enum DeliveryConfirmationStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidState,
    InvalidCode,
    Expired,
    Locked,
    NotConfigured,
    EmailConfigurationInvalid,
    EmailDeliveryFailed,
    Disabled
}

public sealed record DeliveryCodeDispatchResult(
    DeliveryConfirmationStatus Status,
    DeliveryCodeDispatchResponse? Dispatch = null,
    string? Detail = null);

public sealed record VerifyDeliveryCodeResult(
    DeliveryConfirmationStatus Status,
    DeliveryVerificationResponse? Verification = null,
    string? Detail = null);
