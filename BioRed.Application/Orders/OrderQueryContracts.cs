using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Orders;

public sealed class GetOrdersRequest
{
    [RegularExpression(
        "^(Pendiente|Aceptado|En Preparación|En Camino|Entregado)$",
        ErrorMessage = "El estado enviado no es válido.")]
    public string? Status { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record OrderSummaryResponse(
    int OrderId,
    string OrderCode,
    string ClientCode,
    string ClientName,
    string BranchCode,
    string BranchName,
    string? DriverCode,
    string Status,
    decimal Subtotal,
    decimal DeliveryCost,
    decimal TotalCharged,
    DateTime CreatedAtUtc,
    DateTime? DeliveredAtUtc);

public sealed record GetOrdersResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<OrderSummaryResponse> Items);

public sealed record OrderDetailItemResponse(
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    bool RequiresPrescription);

public sealed record OrderHistoryResponse(
    string Status,
    string? Description,
    string? UpdatedBy,
    DateTime RegisteredAtUtc);

public sealed record OrderLocationResponse(
    long LocationId,
    decimal Latitude,
    decimal Longitude,
    DateTime RegisteredAtUtc);

public sealed record OrderDetailResponse(
    int OrderId,
    string OrderCode,
    string ClientCode,
    string ClientName,
    string BranchCode,
    string BranchName,
    string? DriverCode,
    int DeliveryAddressId,
    string DeliveryAddressName,
    string DeliveryAddress,
    string PaymentTypeCode,
    string PaymentTypeName,
    string Status,
    decimal Subtotal,
    decimal DeliveryCost,
    decimal DiscountApplied,
    decimal TotalCharged,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? DeliveredAtUtc,
    IReadOnlyCollection<OrderDetailItemResponse> Items,
    IReadOnlyCollection<OrderHistoryResponse> History,
    OrderLocationResponse? LastLocation,
    string? PromotionCode = null,
    long? PrescriptionId = null);

public enum OrderQueryStatus
{
    Success,
    NotFound,
    Forbidden
}

public sealed record GetOrdersResult(
    OrderQueryStatus Status,
    GetOrdersResponse? Orders = null,
    string? Detail = null);

public sealed record GetOrderDetailResult(
    OrderQueryStatus Status,
    OrderDetailResponse? Order = null,
    string? Detail = null);
