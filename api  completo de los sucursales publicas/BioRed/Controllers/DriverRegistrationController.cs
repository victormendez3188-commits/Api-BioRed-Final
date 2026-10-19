using BioRed.Application.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

/// <summary>
/// Compatibility endpoint for the mobile driver's registration form. Unlike
/// /api/v1/drivers/register, this route accepts the driver's chosen password;
/// the existing temporary-password endpoint remains available for the admin
/// onboarding workflow.
/// </summary>
[ApiController]
[Route("api/v1/auth/register-driver")]
[AllowAnonymous]
[EnableRateLimiting("registration")]
public sealed class DriverRegistrationController : ControllerBase
{
    private readonly IDriverProfileService _driverProfileService;

    public DriverRegistrationController(IDriverProfileService driverProfileService)
    {
        _driverProfileService = driverProfileService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(DriverProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DriverProfileResponse>> RegisterAsync(
        [FromBody] RegisterDriverFromMobileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _driverProfileService.RegisterFromMobileAsync(
            request,
            cancellationToken);

        return result.Status switch
        {
            DriverProfileStatus.Success when result.Profile is not null =>
                StatusCode(StatusCodes.Status201Created, result.Profile),
            DriverProfileStatus.DuplicateEmail => Conflict(CreateProblem(
                StatusCodes.Status409Conflict, "Correo duplicado", result.Detail)),
            DriverProfileStatus.DuplicateDocument => Conflict(CreateProblem(
                StatusCodes.Status409Conflict, "Documento duplicado", result.Detail)),
            DriverProfileStatus.DuplicateLicensePlate => Conflict(CreateProblem(
                StatusCodes.Status409Conflict, "Placa duplicada", result.Detail)),
            DriverProfileStatus.DuplicateLicenseNumber => Conflict(CreateProblem(
                StatusCodes.Status409Conflict, "Licencia duplicada", result.Detail)),
            DriverProfileStatus.DuplicateData => Conflict(CreateProblem(
                StatusCodes.Status409Conflict, "Repartidor duplicado", result.Detail)),
            _ => BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest, "Registro inválido", result.Detail))
        };
    }

    private ProblemDetails CreateProblem(int status, string title, string? detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
