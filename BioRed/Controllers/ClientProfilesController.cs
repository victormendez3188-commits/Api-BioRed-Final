using System.Security.Claims;
using BioRed.Application.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/clients")]
[Authorize(Roles = "CLIENTE")]
public sealed class ClientProfilesController : ControllerBase
{
    private readonly IClientProfileService _profileService;

    public ClientProfilesController(IClientProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("registration")]
    [ProducesResponseType(typeof(ClientProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ClientProfileResponse>> RegisterAsync(
        [FromBody] RegisterClientRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.RegisterAsync(
            request,
            cancellationToken);

        return result.Status switch
        {
            ClientProfileStatus.Success when result.Profile is not null =>
                StatusCode(StatusCodes.Status201Created, result.Profile),

            ClientProfileStatus.BranchNotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Sucursal no encontrada",
                    result.Detail)),

            ClientProfileStatus.DuplicateEmail =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Correo duplicado",
                    result.Detail)),

            ClientProfileStatus.DuplicateDocument =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Documento duplicado",
                    result.Detail)),

            ClientProfileStatus.DuplicateData =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cliente duplicado",
                    result.Detail)),

            ClientProfileStatus.SystemUserNotFound =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Configuración incompleta",
                        result.Detail)),

            ClientProfileStatus.EmailConfigurationInvalid =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no configurado",
                        result.Detail)),

            ClientProfileStatus.EmailDeliveryFailed =>
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
    [ProducesResponseType(typeof(ClientProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientProfileResponse>> GetAsync(
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();
        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.GetAsync(
            clientCode,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ClientProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientProfileResponse>> UpdateAsync(
        [FromBody] UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
    {
        var clientCode = GetClientCode();
        if (clientCode is null)
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateAsync(
            clientCode,
            request,
            cancellationToken);

        return ToActionResult(result);
    }

    private string? GetClientCode() =>
        User.FindFirstValue("reference_id");

    private ActionResult<ClientProfileResponse> ToActionResult(
        ClientProfileResult result)
    {
        if (result.Status == ClientProfileStatus.Success &&
            result.Profile is not null)
        {
            return Ok(result.Profile);
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
