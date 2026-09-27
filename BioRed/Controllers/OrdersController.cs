using System.Security.Claims;
using BioRed.Application.Orders;
using BioRed.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetOrdersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GetOrdersResponse>> GetAsync(
        [FromQuery] GetOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var actorType = User.FindFirstValue("user_type") ?? string.Empty;
        var actorReferenceId = User.FindFirstValue("reference_id") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _orderService.GetAsync(
            actorType,
            actorReferenceId,
            User.IsInRole("ADMIN"),
            User.HasClaim("permission", "PEDIDO_VER"),
            request,
            cancellationToken);

        return result.Status switch
        {
            OrderQueryStatus.Success when result.Orders is not null =>
                Ok(result.Orders),

            _ => Forbid()
        };
    }

    [HttpGet("{orderId:int}")]
    [ProducesResponseType(typeof(OrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailResponse>> GetByIdAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var actorType = User.FindFirstValue("user_type") ?? string.Empty;
        var actorReferenceId = User.FindFirstValue("reference_id") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _orderService.GetByIdAsync(
            orderId,
            actorType,
            actorReferenceId,
            User.IsInRole("ADMIN"),
            User.HasClaim("permission", "PEDIDO_VER"),
            cancellationToken);

        return result.Status switch
        {
            OrderQueryStatus.Success when result.Order is not null =>
                Ok(result.Order),

            OrderQueryStatus.Forbidden =>
                Forbid(),

            _ =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Pedido no encontrado",
                    result.Detail))
        };
    }

    [HttpPost]
    [Authorize(Roles = "CLIENTE")]
    [ProducesResponseType(typeof(CreateOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreateOrderResponse>> CreateAsync(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = User.FindFirstValue("reference_id");

        if (string.IsNullOrWhiteSpace(clientCode))
        {
            return Unauthorized();
        }

        var result = await _orderService.CreateAsync(
            clientCode,
            request,
            cancellationToken);

        return result.Status switch
        {
            CreateOrderStatus.Success when result.Order is not null =>
                StatusCode(StatusCodes.Status201Created, result.Order),

            CreateOrderStatus.InsufficientStock =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Existencias insuficientes",
                    result.Detail)),

            CreateOrderStatus.PromotionNotApplicable or
            CreateOrderStatus.PrescriptionRequired or
            CreateOrderStatus.PrescriptionInvalid =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "No se puede completar el pedido",
                    result.Detail)),

            CreateOrderStatus.OutOfDeliveryArea =>
                UnprocessableEntity(CreateProblem(
                    StatusCodes.Status422UnprocessableEntity,
                    "Dirección fuera de cobertura",
                    result.Detail)),

            CreateOrderStatus.ClientNotFound or
            CreateOrderStatus.BranchNotFound or
            CreateOrderStatus.AddressNotFound or
            CreateOrderStatus.PaymentTypeNotFound or
            CreateOrderStatus.ProductNotFound or
            CreateOrderStatus.PromotionNotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Información no encontrada",
                    result.Detail)),

            _ =>
                BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Solicitud inválida",
                    result.Detail))
        };
    }

    [HttpPatch("{orderId:int}/status")]
    [ProducesResponseType(typeof(UpdateOrderStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateOrderStatusResponse>> UpdateStatusAsync(
        int orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var actorType = User.FindFirstValue("user_type") ?? string.Empty;
        var actorReferenceId = User.FindFirstValue("reference_id") ?? string.Empty;
        var canManageAnyOrder =
            User.IsInRole("ADMIN") ||
            User.HasClaim("permission", "PEDIDO_VER");

        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _orderService.UpdateStatusAsync(
            orderId,
            actorType,
            actorReferenceId,
            canManageAnyOrder,
            request,
            cancellationToken);

        return result.Status switch
        {
            UpdateOrderStatusStatus.Success when result.Order is not null =>
                Ok(result.Order),

            UpdateOrderStatusStatus.Forbidden =>
                Forbid(),

            UpdateOrderStatusStatus.NotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Pedido no encontrado",
                    result.Detail)),

            _ =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Transición de estado inválida",
                    result.Detail))
        };
    }

    [HttpPut("{orderId:int}/driver")]
    [HasPermission("PEDIDO_VER")]
    [ProducesResponseType(typeof(AssignDriverResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignDriverResponse>> AssignDriverAsync(
        int orderId,
        [FromBody] AssignDriverRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");

        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _orderService.AssignDriverAsync(
            orderId,
            actorReferenceId,
            request,
            cancellationToken);

        return result.Status switch
        {
            AssignDriverStatus.Success when result.Assignment is not null =>
                Ok(result.Assignment),

            AssignDriverStatus.OrderNotFound or
            AssignDriverStatus.DriverNotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Información no encontrada",
                    result.Detail)),

            _ =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "No fue posible asignar el repartidor",
                    result.Detail))
        };
    }

    private ProblemDetails CreateProblem(
        int status,
        string title,
        string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
