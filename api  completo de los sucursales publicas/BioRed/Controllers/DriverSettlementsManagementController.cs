using System.Security.Claims;
using BioRed.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/management/driver-settlements")]
[Authorize(Roles = "ADMIN")]
[EnableRateLimiting("driver-payouts")]
public sealed class DriverSettlementsManagementController : ControllerBase
{
    private readonly IDriverSettlementService _service;

    public DriverSettlementsManagementController(IDriverSettlementService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(GetDriverSettlementsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetDriverSettlementsResponse>> GetAsync(
        [FromQuery] GetDriverSettlementsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            true,
            request,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlements is not null
            ? Ok(result.Settlements)
            : MapProblem<GetDriverSettlementsResponse>(result);
    }

    [HttpGet("{settlementId:long}")]
    [ProducesResponseType(typeof(DriverSettlementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementResponse>> GetByIdAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(
            settlementId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            true,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlement is not null
            ? Ok(result.Settlement)
            : MapProblem<DriverSettlementResponse>(result);
    }

    [HttpPost("{settlementId:long}/retry")]
    [ProducesResponseType(typeof(DriverSettlementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementResponse>> RetryAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var result = await _service.RetryPayPalAsync(
            settlementId,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlement is not null
            ? Ok(result.Settlement)
            : MapProblem<DriverSettlementResponse>(result);
    }

    [HttpPost("{settlementId:long}/refresh")]
    [ProducesResponseType(typeof(DriverSettlementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementResponse>> RefreshAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var result = await _service.RefreshPayPalAsync(
            settlementId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            true,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlement is not null
            ? Ok(result.Settlement)
            : MapProblem<DriverSettlementResponse>(result);
    }

    [HttpPost("{settlementId:long}/cash-remittance/confirm")]
    [ProducesResponseType(typeof(DriverSettlementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementResponse>> ConfirmCashRemittanceAsync(
        long settlementId,
        [FromBody] ConfirmDriverCashRemittanceRequest request,
        CancellationToken cancellationToken)
    {
        var administratorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(administratorCode))
        {
            return Unauthorized();
        }

        var result = await _service.ConfirmCashRemittanceAsync(
            settlementId,
            administratorCode,
            request,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlement is not null
            ? Ok(result.Settlement)
            : MapProblem<DriverSettlementResponse>(result);
    }

    /// <summary>
    /// Devuelve los ajustes creados cuando un reembolso incluye el costo de
    /// entrega. Solo el administrador puede consultar el historial completo.
    /// </summary>
    [HttpGet("{settlementId:long}/refund-adjustments")]
    [ProducesResponseType(
        typeof(GetDriverSettlementRefundAdjustmentsResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<GetDriverSettlementRefundAdjustmentsResponse>>
        GetRefundAdjustmentsAsync(
            long settlementId,
            CancellationToken cancellationToken)
    {
        var result = await _service.GetRefundAdjustmentsAsync(
            settlementId,
            User.FindFirstValue("user_type") ?? string.Empty,
            User.FindFirstValue("reference_id") ?? string.Empty,
            true,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.RefundAdjustments is not null
            ? Ok(result.RefundAdjustments)
            : MapProblem<GetDriverSettlementRefundAdjustmentsResponse>(result);
    }

    /// <summary>
    /// Confirma la recuperación manual cuando el pago de entrega ya había sido
    /// conciliado en efectivo o acreditado por PayPal.
    /// </summary>
    [HttpPost("refund-adjustments/{adjustmentId:long}/confirm")]
    [ProducesResponseType(
        typeof(DriverSettlementRefundAdjustmentResponse),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementRefundAdjustmentResponse>>
        ConfirmRefundRecoveryAsync(
            long adjustmentId,
            [FromBody] ConfirmDriverRefundRecoveryRequest request,
            CancellationToken cancellationToken)
    {
        var administratorCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(administratorCode))
        {
            return Unauthorized();
        }

        var result = await _service.ConfirmRefundRecoveryAsync(
            adjustmentId,
            administratorCode,
            request,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.RefundAdjustment is not null
            ? Ok(result.RefundAdjustment)
            : MapProblem<DriverSettlementRefundAdjustmentResponse>(result);
    }

    private ActionResult<T> MapProblem<T>(DriverSettlementResult result)
    {
        if (result.Status == DriverSettlementStatus.Forbidden)
        {
            return Forbid();
        }

        var (status, title) = result.Status switch
        {
            DriverSettlementStatus.NotFound =>
                (404, "Información no encontrada"),
            DriverSettlementStatus.ConfigurationError =>
                (503, "Liquidación no configurada"),
            DriverSettlementStatus.ProviderError =>
                (502, "Error del proveedor de pagos"),
            _ =>
                (409, "No se puede completar la liquidación")
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
            404 => NotFound(problem),
            502 => StatusCode(502, problem),
            503 => StatusCode(503, problem),
            _ => Conflict(problem)
        };
    }
}
