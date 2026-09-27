using BioRed.Application.Security;
using BioRed.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace BioRed.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const int Status423Locked = 423;

    private const string ProductViewPermission =
        "PRODUCTO_VER";

    private readonly IAuthenticationService
        _authenticationService;

    private readonly ITokenRefreshService
        _tokenRefreshService;
    private readonly IAccountManagementService _accountManagementService;

    public AuthController(
        IAuthenticationService authenticationService,
        ITokenRefreshService tokenRefreshService,
        IAccountManagementService accountManagementService)
    {
        _authenticationService = authenticationService;
        _tokenRefreshService = tokenRefreshService;
        _accountManagementService = accountManagementService;
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _accountManagementService.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Ok(new { message = "Si existe una cuenta activa, enviaremos una contraseña temporal a su correo registrado." });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync([FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _tokenRefreshService.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("authentication")]
    [ProducesResponseType(
        typeof(LoginResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        Status423Locked)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress =
            HttpContext.Connection.RemoteIpAddress?.ToString();

        var userAgent =
            Request.Headers["User-Agent"].ToString();

        var result =
            await _authenticationService.LoginAsync(
                request,
                ipAddress,
                userAgent,
                cancellationToken);

        switch (result.Status)
        {
            case AuthenticationStatus.Success
                when result.Response is not null:

                return Ok(result.Response);

            case AuthenticationStatus.LockedOut:

                return StatusCode(
                    Status423Locked,
                    CreateProblem(
                        Status423Locked,
                        "Cuenta bloqueada",
                        "La cuenta se encuentra bloqueada temporalmente.",
                        "ACCOUNT_LOCKED",
                        result.LockedUntilUtc));

            case AuthenticationStatus.PasswordChangeRequired:

                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblem(
                        StatusCodes.Status403Forbidden,
                        "Cambio de contraseña requerido",
                        "Debe cambiar su contraseña antes de continuar.",
                        "PASSWORD_CHANGE_REQUIRED"));

            case AuthenticationStatus.TemporaryPasswordExpired:

                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblem(
                        StatusCodes.Status403Forbidden,
                        "Contraseña temporal vencida",
                        "Solicite a administración una nueva contraseña temporal.",
                        "TEMPORARY_PASSWORD_EXPIRED"));

            case AuthenticationStatus.TwoFactorRequired:

                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblem(
                        StatusCodes.Status403Forbidden,
                        "Segundo factor requerido",
                        "Debe completar la autenticación de dos factores.",
                        "TWO_FACTOR_REQUIRED"));

            case AuthenticationStatus.InvalidCredentials:
            case AuthenticationStatus.InactiveUser:

                return Unauthorized(
                    CreateProblem(
                        StatusCodes.Status401Unauthorized,
                        "Acceso no autorizado",
                        "El usuario, correo o contraseña no son válidos.",
                        "INVALID_CREDENTIALS"));

            default:

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    CreateProblem(
                        StatusCodes.Status500InternalServerError,
                        "Error interno",
                        "No fue posible completar el inicio de sesión.",
                        "AUTHENTICATION_ERROR"));
        }
    }

    [AllowAnonymous]
    [HttpPost("change-temporary-password")]
    [EnableRateLimiting("authentication")]
    [ProducesResponseType(
        typeof(TemporaryPasswordChangeResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status410Gone)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TemporaryPasswordChangeResponse>>
        ChangeTemporaryPasswordAsync(
            [FromBody] ChangeTemporaryPasswordRequest request,
            CancellationToken cancellationToken)
    {
        var result = await _authenticationService.ChangeTemporaryPasswordAsync(
            request,
            cancellationToken);

        return result.Status switch
        {
            TemporaryPasswordChangeStatus.Success when result.Response is not null =>
                Ok(result.Response),

            TemporaryPasswordChangeStatus.Expired =>
                StatusCode(
                    StatusCodes.Status410Gone,
                    CreateProblem(
                        StatusCodes.Status410Gone,
                        "Contraseña temporal vencida",
                        result.Detail ?? "Solicite una nueva contraseña temporal.",
                        "TEMPORARY_PASSWORD_EXPIRED")),

            TemporaryPasswordChangeStatus.ChangeNotRequired =>
                Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cambio no requerido",
                    result.Detail ?? "La cuenta no requiere este cambio.",
                    "PASSWORD_CHANGE_NOT_REQUIRED")),

            TemporaryPasswordChangeStatus.InvalidNewPassword =>
                BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Nueva contraseña insegura",
                    result.Detail ?? "La nueva contraseña no cumple los requisitos.",
                    "INVALID_NEW_PASSWORD")),

            _ =>
                Unauthorized(CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Datos no válidos",
                    "El correo o la contraseña temporal no son válidos.",
                    "INVALID_TEMPORARY_CREDENTIALS"))
        };
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("authentication")]
    [ProducesResponseType(
        typeof(TokenRefreshResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenRefreshResponse>>
        RefreshAsync(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
    {
        var ipAddress =
            HttpContext.Connection.RemoteIpAddress?.ToString();

        var result =
            await _tokenRefreshService.RefreshAsync(
                request,
                ipAddress,
                cancellationToken);

        if (result.Status == TokenRefreshStatus.Success &&
            result.Response is not null)
        {
            return Ok(result.Response);
        }

        return Unauthorized(
            CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Refresh Token inválido",
                "El Refresh Token expiró, fue revocado o ya fue utilizado.",
                "INVALID_REFRESH_TOKEN"));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(
        typeof(CurrentUserResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> GetCurrentUser()
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role)
            .ToArray();

        var permissions = User.FindAll("permission")
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission)
            .ToArray();

        var response = new CurrentUserResponse(
            UserId: userId,
            UserName:
                User.FindFirstValue(ClaimTypes.Name)
                ?? string.Empty,
            Email:
                User.FindFirstValue(ClaimTypes.Email),
            FullName:
                User.FindFirstValue("full_name"),
            UserType:
                User.FindFirstValue("user_type"),
            ReferenceId:
                User.FindFirstValue("reference_id"),
            Roles: roles,
            Permissions: permissions);

        return Ok(response);
    }

    [HasPermission(ProductViewPermission)]
    [HttpGet("permission-check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult CheckPermission()
    {
        return Ok(new
        {
            message =
                "El permiso fue validado correctamente.",
            permission = ProductViewPermission
        });
    }

    private ProblemDetails CreateProblem(
        int status,
        string title,
        string detail,
        string code,
        DateTime? lockedUntilUtc = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path.Value
        };

        problem.Extensions["code"] = code;

        if (lockedUntilUtc.HasValue)
        {
            problem.Extensions["lockedUntilUtc"] =
                lockedUntilUtc.Value;
        }

        return problem;
    }
}
