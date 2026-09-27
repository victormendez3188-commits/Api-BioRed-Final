using System.Security.Claims;
using BioRed.Application.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/drivers")]
[Authorize(Roles = "REPARTIDOR")]
public sealed class DriverProfilesController : ControllerBase
{
    private readonly IDriverProfileService _profileService;

    public DriverProfilesController(IDriverProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(DriverProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DriverProfileResponse>> RegisterAsync(
        [FromBody] RegisterDriverRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.RegisterAsync(
            request,
            cancellationToken);

        return result.Status switch
        {
            DriverProfileStatus.Success when result.Profile is not null =>
                StatusCode(StatusCodes.Status201Created, result.Profile),

            DriverProfileStatus.DuplicateEmail =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Correo duplicado",
                    result.Detail)),

            DriverProfileStatus.DuplicateDocument =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Documento duplicado",
                    result.Detail)),

            DriverProfileStatus.DuplicateLicensePlate =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Placa duplicada",
                    result.Detail)),

            DriverProfileStatus.DuplicateData =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Repartidor duplicado",
                    result.Detail)),

            DriverProfileStatus.EmailConfigurationInvalid =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no configurado",
                        result.Detail)),

            DriverProfileStatus.EmailDeliveryFailed =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no enviado",
                        result.Detail)),

            _ =>
                BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Solicitud no válida",
                    result.Detail))
        };
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(DriverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverProfileResponse>> GetAsync(
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.GetAsync(
            driverCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(DriverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverProfileResponse>> UpdateAsync(
        [FromBody] UpdateDriverProfileRequest request,
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateAsync(
            driverCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPatch("me/availability")]
    [ProducesResponseType(typeof(DriverProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverProfileResponse>> UpdateAvailabilityAsync(
        [FromBody] UpdateDriverAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var driverCode = GetDriverCode();
        if (driverCode is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateAvailabilityAsync(
            driverCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    private string? GetDriverCode() =>
        User.FindFirstValue("reference_id");

    private ActionResult<DriverProfileResponse> ToActionResult(
        DriverProfileResult result)
    {
        if (result.Status == DriverProfileStatus.Success &&
            result.Profile is not null)
        {
            return Ok(result.Profile);
        }

        if (result.Status == DriverProfileStatus.DuplicateLicensePlate)
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Placa duplicada",
                result.Detail));
        }

        if (result.Status == DriverProfileStatus.Busy)
        {
            return Conflict(CreateProblem(
                StatusCodes.Status409Conflict,
                "Repartidor ocupado",
                result.Detail));
        }

        return NotFound(CreateProblem(
            StatusCodes.Status404NotFound,
            "Perfil no encontrado",
            result.Detail));
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
