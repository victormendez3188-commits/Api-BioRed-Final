using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Orders;

public sealed class CreateOrderRequest
{
    [Required]
    [StringLength(15)]
    public string BranchCode { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DeliveryAddressId { get; init; }

    [Required]
    [StringLength(15)]
    public string PaymentTypeCode { get; init; } = string.Empty;

    [StringLength(15)]
    public string? PromotionCode { get; init; }

    [Range(1, long.MaxValue)]
    public long? PrescriptionId { get; init; }

    [StringLength(255)]
    public string? Notes { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "Debe agregar al menos un producto.")]
    [MaxLength(100, ErrorMessage = "No puede enviar más de 100 productos diferentes.")]
    public IReadOnlyCollection<CreateOrderItemRequest> Items { get; init; } =
        Array.Empty<CreateOrderItemRequest>();
}

public sealed class CreateOrderItemRequest
{
    [Required]
    [StringLength(15)]
    public string ProductCode { get; init; } = string.Empty;

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

public sealed record OrderItemResponse(
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record CreateOrderResponse(
    int OrderId,
    string OrderCode,
    string ClientCode,
    string BranchCode,
    int DeliveryAddressId,
    string Status,
    decimal Subtotal,
    decimal DeliveryCost,
    decimal TotalCharged,
    DateTime CreatedAtUtc,
    IReadOnlyCollection<OrderItemResponse> Items,
    decimal DiscountApplied = 0m,
    string? PromotionCode = null,
    long? PrescriptionId = null);

public enum CreateOrderStatus
{
    Success,
    InvalidRequest,
    ClientNotFound,
    BranchNotFound,
    AddressNotFound,
    PaymentTypeNotFound,
    ProductNotFound,
    PromotionNotFound,
    PromotionNotApplicable,
    PrescriptionRequired,
    PrescriptionInvalid,
    InsufficientStock,
    OutOfDeliveryArea
}

public sealed record CreateOrderResult(
    CreateOrderStatus Status,
    CreateOrderResponse? Order = null,
    string? Detail = null);

public interface IOrderService
{
    Task<GetOrdersResult> GetAsync(
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        bool canViewManagedOrders,
        GetOrdersRequest request,
        CancellationToken cancellationToken = default);

    Task<GetOrderDetailResult> GetByIdAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        bool canViewManagedOrders,
        CancellationToken cancellationToken = default);

    Task<CreateOrderResult> CreateAsync(
        string clientCode,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateOrderStatusResult> UpdateStatusAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool canManageAnyOrder,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<AssignDriverResult> AssignDriverAsync(
        int orderId,
        string actorReferenceId,
        AssignDriverRequest request,
        CancellationToken cancellationToken = default);
}
