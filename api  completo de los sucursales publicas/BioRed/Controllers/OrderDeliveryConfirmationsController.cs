using System.Security.Claims;
using BioRed.Application.Delivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

/// <summary>
/// Delivers and validates a one-time four-digit code without exposing it from
/// the API. The client receives it through the registered email address.
/// </summary>
[ApiController]
[Route("api/v1/orders/{orderId:int}/delivery-code")]
[Authorize]
[EnableRateLimiting("delivery-code")]
public sealed class OrderDeliveryConfirmationsController : ControllerBase
{
    private readonly IOrderDeliveryConfirmationService _service;

    public OrderDeliveryConfirmationsController(
        IOrderDeliveryConfirmationService service)
    {
        _service = service;
    }

    [HttpPost("resend")]
    [Authorize(Roles = "CLIENTE,ADMIN")]
    [ProducesResponseType(typeof(DeliveryCodeDispatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DeliveryCodeDispatchResponse>> ResendAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized();
        }

        var result = await _service.ResendAsync(
            orderId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            User.IsInRole("ADMIN"),
            cancellationToken);

        return ToDispatchActionResult(result);
    }

    [HttpPost("verify")]
    [Authorize(Roles = "REPARTIDOR")]
    [ProducesResponseType(typeof(DeliveryVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryVerificationResponse>> VerifyAsync(
        int orderId,
        [FromBody] VerifyDeliveryCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(out var accountId))
        {
            return Unauthorized();
        }

        var result = await _service.VerifyAsync(
            orderId,
            accountId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            request.Code,
            cancellationToken);

        return result.Status switch
        {
            DeliveryConfirmationStatus.Success when result.Verification is not null => Ok(result.Verification),
            DeliveryConfirmationStatus.Forbidden => Forbid(),
            DeliveryConfirmationStatus.NotFound => NotFound(CreateProblem(404, "Pedido no encontrado", result.Detail)),
            DeliveryConfirmationStatus.InvalidState or
            DeliveryConfirmationStatus.Expired or
            DeliveryConfirmationStatus.Locked or
            DeliveryConfirmationStatus.NotConfigured => Conflict(CreateProblem(409, "No se puede validar la entrega", result.Detail)),
            _ => BadRequest(CreateProblem(400, "Código de entrega inválido", result.Detail))
        };
    }

    private ActionResult<DeliveryCodeDispatchResponse> ToDispatchActionResult(
        DeliveryCodeDispatchResult result) =>
        result.Status switch
        {
            DeliveryConfirmationStatus.Success when result.Dispatch is not null => Ok(result.Dispatch),
            DeliveryConfirmationStatus.Forbidden => Forbid(),
            DeliveryConfirmationStatus.NotFound => NotFound(CreateProblem(404, "Pedido no encontrado", result.Detail)),
            DeliveryConfirmationStatus.InvalidState => Conflict(CreateProblem(409, "No se puede reenviar el código", result.Detail)),
            DeliveryConfirmationStatus.Disabled or
            DeliveryConfirmationStatus.EmailConfigurationInvalid or
            DeliveryConfirmationStatus.EmailDeliveryFailed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                CreateProblem(503, "Código de entrega no disponible", result.Detail)),
            _ => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail))
        };

    private bool TryGetAccountId(out int accountId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out accountId) && accountId > 0;

    private ProblemDetails CreateProblem(int status, string title, string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
