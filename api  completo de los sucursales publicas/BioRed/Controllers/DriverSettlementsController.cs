using System.Security.Claims;
using BioRed.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/drivers/me")]
[Authorize(Roles = "REPARTIDOR")]
[EnableRateLimiting("driver-payouts")]
public sealed class DriverSettlementsController : ControllerBase
{
    private readonly IDriverSettlementService _service;

    public DriverSettlementsController(IDriverSettlementService service)
    {
        _service = service;
    }

    [HttpGet("payout-account")]
    [ProducesResponseType(typeof(DriverPayoutAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverPayoutAccountResponse>> GetPayoutAccountAsync(
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _service.GetPayoutAccountAsync(
            driverCode,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Account is not null
            ? Ok(result.Account)
            : MapProblem<DriverPayoutAccountResponse>(result);
    }

    [HttpPut("payout-account")]
    [ProducesResponseType(typeof(DriverPayoutAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverPayoutAccountResponse>> UpsertPayoutAccountAsync(
        [FromBody] UpsertDriverPayoutAccountRequest request,
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _service.UpsertPayoutAccountAsync(
            driverCode,
            request,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Account is not null
            ? Ok(result.Account)
            : MapProblem<DriverPayoutAccountResponse>(result);
    }

    [HttpGet("settlements")]
    [ProducesResponseType(typeof(GetDriverSettlementsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GetDriverSettlementsResponse>> GetSettlementsAsync(
        [FromQuery] GetDriverSettlementsRequest request,
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _service.GetAsync(
            "Repartidor",
            driverCode,
            false,
            request,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlements is not null
            ? Ok(result.Settlements)
            : MapProblem<GetDriverSettlementsResponse>(result);
    }

    [HttpGet("settlements/{settlementId:long}")]
    [ProducesResponseType(typeof(DriverSettlementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementResponse>> GetSettlementAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _service.GetByIdAsync(
            settlementId,
            "Repartidor",
            driverCode,
            false,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlement is not null
            ? Ok(result.Settlement)
            : MapProblem<DriverSettlementResponse>(result);
    }

    [HttpPost("settlements/{settlementId:long}/refresh")]
    [ProducesResponseType(typeof(DriverSettlementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DriverSettlementResponse>> RefreshSettlementAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _service.RefreshPayPalAsync(
            settlementId,
            "Repartidor",
            driverCode,
            false,
            cancellationToken);
        return result.Status == DriverSettlementStatus.Success &&
               result.Settlement is not null
            ? Ok(result.Settlement)
            : MapProblem<DriverSettlementResponse>(result);
    }

    private string? GetDriverCode() =>
        User.FindFirstValue("reference_id");

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
            DriverSettlementStatus.DuplicatePayoutAccount =>
                (409, "Cuenta PayPal duplicada"),
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
