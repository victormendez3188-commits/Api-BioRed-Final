using System.Security.Claims;
using BioRed.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/payments")]
[Authorize]
public sealed class PaymentsManagementController : ControllerBase
{
    private readonly IPaymentService _service;

    public PaymentsManagementController(IPaymentService service)
    {
        _service = service;
    }

    [HttpPost("orders/{orderId:int}/cash/confirm")]
    [Authorize(Roles = "ADMIN,REPARTIDOR")]
    public async Task<ActionResult<PaymentResponse>> ConfirmCashAsync(
        int orderId,
        [FromBody] ConfirmCashPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId) ||
            !int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var actorAccountId))
        {
            return Unauthorized();
        }

        var result = await _service.ConfirmCashAsync(
            orderId,
            User.FindFirstValue("user_type") ?? string.Empty,
            actorReferenceId,
            actorAccountId,
            User.IsInRole("ADMIN"),
            request,
            cancellationToken);

        return result.Status == PaymentManagementStatus.Success &&
               result.Payment is not null
            ? Ok(result.Payment)
            : MapProblem<PaymentResponse>(result);
    }

    [HttpPost("refunds")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<RefundResponse>> ProcessRefundAsync(
        [FromBody] ProcessRefundRequest request,
        CancellationToken cancellationToken)
    {
        var actorReferenceId = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return Unauthorized();
        }

        var result = await _service.ProcessRefundAsync(
            actorReferenceId,
            request,
            cancellationToken);

        return result.Status == PaymentManagementStatus.Success &&
               result.Refund is not null
            ? Ok(result.Refund)
            : MapProblem<RefundResponse>(result);
    }

    private ActionResult<T> MapProblem<T>(PaymentManagementResult result)
    {
        if (result.Status == PaymentManagementStatus.Forbidden)
        {
            return Forbid();
        }

        var (status, title) = result.Status switch
        {
            PaymentManagementStatus.NotFound =>
                (404, "Información no encontrada"),
            PaymentManagementStatus.ConfigurationError =>
                (503, "Integración de pagos no configurada"),
            PaymentManagementStatus.ProviderError =>
                (502, "Error del proveedor de pagos"),
            PaymentManagementStatus.InvalidAmount or
            PaymentManagementStatus.CashReferenceRequired =>
                (400, "Monto de pago inválido"),
            _ =>
                (409, "No se puede completar la operación de pago")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Detail,
            Instance = HttpContext.Request.Path.Value
        };

        return status switch
        {
            400 => BadRequest(problem),
            404 => NotFound(problem),
            502 => StatusCode(502, problem),
            503 => StatusCode(503, problem),
            _ => Conflict(problem)
        };
    }
}
