using System.Security.Claims;
using BioRed.Application.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/orders/{orderId:int}/route")]
[Authorize(Roles = "REPARTIDOR")]
[EnableRateLimiting("routing")]
public sealed class OrderRoutesController : ControllerBase
{
    private readonly IOrderRouteService _service;

    public OrderRoutesController(IOrderRouteService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RouteResponse>> GetAsync(
        int orderId,
        [FromQuery] decimal originLatitude,
        [FromQuery] decimal originLongitude,
        [FromQuery] string travelMode = "TWO_WHEELER",
        CancellationToken cancellationToken = default)
    {
        var driverCode = User.FindFirstValue("reference_id");
        if (string.IsNullOrWhiteSpace(driverCode))
        {
            return Unauthorized();
        }

        var result = await _service.GetAsync(
            orderId,
            User.FindFirstValue("user_type") ?? string.Empty,
            driverCode,
            originLatitude,
            originLongitude,
            travelMode,
            cancellationToken);

        return result.Status switch
        {
            OrderRouteStatus.Success when result.Route is not null => Ok(result.Route),
            OrderRouteStatus.InvalidRequest => BadRequest(CreateProblem(400, "Solicitud inválida", result.Detail)),
            OrderRouteStatus.Forbidden => Forbid(),
            OrderRouteStatus.NotFound => NotFound(CreateProblem(404, "Ruta no encontrada", result.Detail)),
            OrderRouteStatus.InvalidState => Conflict(CreateProblem(409, "El pedido no permite calcular la ruta", result.Detail)),
            _ => StatusCode(503, CreateProblem(503, "Servicio de rutas no disponible", result.Detail))
        };
    }

    private ProblemDetails CreateProblem(int status, string title, string? detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path.Value
    };
}
