using BioRed.Application.Reporting;
using BioRed.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("dashboard")]
    [HasPermission("REPORTE_VER")]
    [ProducesResponseType(typeof(DashboardReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardReportResponse>> GetDashboardAsync(
        [FromQuery] DashboardReportRequest request,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetDashboardAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("sales/daily")]
    [HasPermission("REPORTE_VER")]
    [ProducesResponseType(typeof(DailySalesReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DailySalesReportResponse>> GetDailySalesAsync(
        [FromQuery] DailySalesReportRequest request,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetDailySalesAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("profitability")]
    [HasPermission("REPORTE_VER")]
    [ProducesResponseType(typeof(ProfitabilityReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProfitabilityReportResponse>> GetProfitabilityAsync(
        [FromQuery] ProfitabilityReportRequest request,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetProfitabilityAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("profitability/orders/{orderId:int}")]
    [HasPermission("REPORTE_VER")]
    [ProducesResponseType(typeof(OrderProfitabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderProfitabilityResponse>> GetOrderProfitabilityAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetOrderProfitabilityAsync(orderId, cancellationToken);

        if (report is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Rentabilidad no encontrada",
                Detail = "El pedido no existe o todavía no tiene un pago completado.",
                Instance = HttpContext.Request.Path.Value
            });
        }

        return Ok(report);
    }

    [HttpGet("inventory/low-stock")]
    [HasPermission("REPORTE_VER")]
    [ProducesResponseType(typeof(LowStockReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LowStockReportResponse>> GetLowStockAsync(
        [FromQuery] LowStockReportRequest request,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetLowStockAsync(request, cancellationToken);
        return Ok(report);
    }

    [HttpGet("audit")]
    [HasPermission("BITACORA_VER")]
    [ProducesResponseType(typeof(AuditReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditReportResponse>> GetAuditAsync(
        [FromQuery] AuditReportRequest request,
        CancellationToken cancellationToken)
    {
        var report = await _reportingService.GetAuditAsync(request, cancellationToken);
        return Ok(report);
    }
}
