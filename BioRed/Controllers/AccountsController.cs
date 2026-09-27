using BioRed.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Authorize(Roles = "ADMIN")]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountManagementService _accountManagementService;

    public AccountsController(IAccountManagementService accountManagementService)
    {
        _accountManagementService = accountManagementService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AccountResponse>> CreateAsync(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _accountManagementService.CreateAsync(
            request,
            cancellationToken);

        return result.Status switch
        {
            CreateAccountStatus.Success when result.Account is not null =>
                StatusCode(StatusCodes.Status201Created, result.Account),

            CreateAccountStatus.Duplicate =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cuenta duplicada",
                    "Ya existe una cuenta con ese correo o referencia.")),

            CreateAccountStatus.ReferenceNotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Referencia inexistente",
                    "No existe un empleado, cliente o repartidor activo con ese código.")),

            CreateAccountStatus.EmailConfigurationInvalid =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no configurado",
                        "Configure el correo SMTP antes de crear cuentas temporales.")),

            CreateAccountStatus.EmailDeliveryFailed =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no enviado",
                        "La cuenta fue creada, pero no se pudo enviar la contraseña temporal. Use el endpoint de reenvío.")),

            _ =>
                BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Solicitud no válida",
                    "No fue posible crear la cuenta."))
        };
    }

    [HttpPost("{accountId:int}/temporary-password")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AccountResponse>> ReissueTemporaryPasswordAsync(
        int accountId,
        CancellationToken cancellationToken)
    {
        var result = await _accountManagementService.ReissueTemporaryPasswordAsync(
            accountId,
            cancellationToken);

        return result.Status switch
        {
            CreateAccountStatus.Success when result.Account is not null =>
                Ok(result.Account),

            CreateAccountStatus.NotFound =>
                NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Cuenta no encontrada",
                    "No existe la cuenta indicada.")),

            CreateAccountStatus.EmailConfigurationInvalid =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no configurado",
                        "Configure el correo SMTP antes de reenviar contraseñas temporales.")),

            _ =>
                StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    CreateProblem(
                        StatusCodes.Status503ServiceUnavailable,
                        "Correo no enviado",
                        "Se generó una nueva contraseña temporal, pero no fue posible enviarla."))
        };
    }

    private ProblemDetails CreateProblem(int status, string title, string detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };
}
