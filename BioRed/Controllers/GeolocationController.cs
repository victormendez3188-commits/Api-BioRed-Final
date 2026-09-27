using System.Security.Claims;
using BioRed.Application.Geolocation;
using BioRed.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/geolocation")]
public sealed class GeolocationController : ControllerBase
{
    private readonly IGeolocationService _geolocationService;

    public GeolocationController(IGeolocationService geolocationService)
    {
        _geolocationService = geolocationService;
    }

    [AllowAnonymous]
    [HttpGet("branches/nearby")]
    [ProducesResponseType(typeof(IReadOnlyCollection<NearbyBranchResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<NearbyBranchResponse>>> GetNearbyBranchesAsync(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        [FromQuery] decimal maximumDistanceKm = 25,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 ||
            longitude is < -180 or > 180 ||
            maximumDistanceKm is <= 0 or > 100)
        {
            ModelState.AddModelError(
                "coordinates",
                "Verifique la latitud, longitud y distancia máxima.");

            return ValidationProblem(ModelState);
        }

        var branches = await _geolocationService.GetNearbyBranchesAsync(
            latitude,
            longitude,
            maximumDistanceKm,
            cancellationToken);

        return Ok(branches);
    }

    [Authorize(Roles = "REPARTIDOR")]
    [HttpPost("drivers/location")]
    [ProducesResponseType(typeof(DriverLocationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverLocationResponse>> UpdateDriverLocationAsync(
        [FromBody] UpdateDriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        var driverCode = User.FindFirstValue("reference_id");

        if (string.IsNullOrWhiteSpace(driverCode))
        {
            return Unauthorized();
        }

        var location = await _geolocationService.UpdateDriverLocationAsync(
            driverCode,
            request,
            cancellationToken);

        return location is null
            ? NotFound(new { message = "No se encontró el repartidor o el pedido asignado." })
            : StatusCode(StatusCodes.Status201Created, location);
    }

    [Authorize]
    [HttpGet("orders/{orderId:int}/tracking")]
    [ProducesResponseType(typeof(OrderTrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderTrackingResponse>> GetOrderTrackingAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var userType = User.FindFirstValue("user_type") ?? string.Empty;
        var referenceId = User.FindFirstValue("reference_id") ?? string.Empty;
        var canViewAnyOrder =
            User.IsInRole("ADMIN") ||
            User.HasClaim("permission", "UBICACION_VER") ||
            User.HasClaim("permission", "PEDIDO_VER");

        var result = await _geolocationService.GetOrderTrackingAsync(
            orderId,
            userType,
            referenceId,
            canViewAnyOrder,
            cancellationToken);

        return result.Status switch
        {
            TrackingQueryStatus.Success when result.Tracking is not null => Ok(result.Tracking),
            TrackingQueryStatus.Forbidden => Forbid(),
            _ => NotFound(new { message = "No se encontró el seguimiento del pedido." })
        };
    }

    [Authorize]
    [HttpGet("orders/{orderId:int}/position")]
    [ProducesResponseType(typeof(OrderPositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderPositionResponse>> GetOrderPositionAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        var userType = User.FindFirstValue("user_type") ?? string.Empty;
        var referenceId = User.FindFirstValue("reference_id") ?? string.Empty;
        var canViewAnyOrder =
            User.IsInRole("ADMIN") ||
            User.HasClaim("permission", "UBICACION_VER") ||
            User.HasClaim("permission", "PEDIDO_VER");

        var result = await _geolocationService.GetOrderPositionAsync(
            orderId,
            userType,
            referenceId,
            canViewAnyOrder,
            cancellationToken);

        return result.Status switch
        {
            TrackingQueryStatus.Success when result.Position is not null => Ok(result.Position),
            TrackingQueryStatus.Forbidden => Forbid(),
            _ => NotFound(new
            {
                message = "No se encontró la geolocalización completa del pedido. " +
                          "Verifique la sucursal, dirección y coordenadas."
            })
        };
    }

    [Authorize]
    [HasPermission("UBICACION_VER")]
    [HttpGet("drivers/positions")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ActiveDriverPositionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<ActiveDriverPositionResponse>>> GetDriverPositionsAsync(
        [FromQuery] string? branchCode,
        CancellationToken cancellationToken)
    {
        if (branchCode?.Length > 15)
        {
            ModelState.AddModelError("branchCode", "El código de sucursal no puede exceder 15 caracteres.");
            return ValidationProblem(ModelState);
        }

        var positions = await _geolocationService.GetActiveDriverPositionsAsync(
            branchCode,
            cancellationToken);

        return Ok(positions);
    }
}
