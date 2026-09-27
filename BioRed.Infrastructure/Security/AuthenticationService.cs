using BioRed.Application.Security;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Security;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly BioRedDbContext _dbContext;
    private readonly IPasswordHasher<CuentaAcceso> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccountIdentityService _accountIdentityService;

    public AuthenticationService(
        BioRedDbContext dbContext,
        IPasswordHasher<CuentaAcceso> passwordHasher,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IAccountIdentityService accountIdentityService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _accountIdentityService = accountIdentityService;
    }

    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = ipAddress;
        _ = userAgent;

        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthenticationResult(AuthenticationStatus.InvalidCredentials);
        }

        var identifier = request.UserNameOrEmail.Trim();

        var account = await _dbContext.CuentasAcceso
            .SingleOrDefaultAsync(
                item => item.Correo == identifier,
                cancellationToken);

        if (account is null)
        {
            var employeeCode = await _dbContext.Usuarios
                .AsNoTracking()
                .Where(item => item.NombreUsuario == identifier)
                .Select(item => item.CodUsuario)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                account = await _dbContext.CuentasAcceso
                    .SingleOrDefaultAsync(
                        item =>
                            item.TipoUsuario == "Empleado" &&
                            item.ReferenciaId == employeeCode,
                        cancellationToken);
            }
        }

        if (account is null)
        {
            return new AuthenticationResult(AuthenticationStatus.InvalidCredentials);
        }

        if (!account.Estado)
        {
            return new AuthenticationResult(AuthenticationStatus.InactiveUser);
        }

        var passwordResult = _passwordHasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return new AuthenticationResult(AuthenticationStatus.InvalidCredentials);
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = _passwordHasher.HashPassword(account, request.Password);
        }

        if (account.RequiereCambioPassword)
        {
            if (!account.PasswordTemporalExpiraUtc.HasValue ||
                account.PasswordTemporalExpiraUtc.Value <= DateTime.UtcNow)
            {
                return new AuthenticationResult(
                    AuthenticationStatus.TemporaryPasswordExpired);
            }

            return new AuthenticationResult(
                AuthenticationStatus.PasswordChangeRequired);
        }

        var authenticatedUser =
            await _accountIdentityService.CreateAuthenticatedUserAsync(
                account,
                cancellationToken);

        if (authenticatedUser is null)
        {
            return new AuthenticationResult(AuthenticationStatus.InactiveUser);
        }

        var accessToken = _tokenService.CreateAccessToken(authenticatedUser);
        var refreshToken = _refreshTokenService.CreateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            IdCuenta = account.IdCuenta,
            Token = refreshToken.TokenHash,
            FechaCreacion = refreshToken.CreatedAtUtc,
            FechaExpiracion = refreshToken.ExpiresAtUtc,
            Revocado = false
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(
            "Bearer",
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc,
            authenticatedUser.UserId,
            authenticatedUser.UserName,
            authenticatedUser.Email,
            authenticatedUser.FullName,
            authenticatedUser.UserType,
            authenticatedUser.ReferenceId,
            authenticatedUser.Roles,
            authenticatedUser.Permissions);

        return new AuthenticationResult(AuthenticationStatus.Success, response);
    }

    public async Task<TemporaryPasswordChangeResult> ChangeTemporaryPasswordAsync(
        ChangeTemporaryPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _dbContext.CuentasAcceso
            .SingleOrDefaultAsync(
                item => item.Correo == email,
                cancellationToken);

        if (account is null || !account.Estado)
        {
            return new TemporaryPasswordChangeResult(
                TemporaryPasswordChangeStatus.InvalidCredentials);
        }

        if (!account.RequiereCambioPassword)
        {
            return new TemporaryPasswordChangeResult(
                TemporaryPasswordChangeStatus.ChangeNotRequired,
                Detail: "La cuenta no tiene un cambio de contraseña temporal pendiente.");
        }

        var verification = _passwordHasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            request.TemporaryPassword);

        if (verification == PasswordVerificationResult.Failed)
        {
            return new TemporaryPasswordChangeResult(
                TemporaryPasswordChangeStatus.InvalidCredentials);
        }

        if (!account.PasswordTemporalExpiraUtc.HasValue ||
            account.PasswordTemporalExpiraUtc.Value <= DateTime.UtcNow)
        {
            return new TemporaryPasswordChangeResult(
                TemporaryPasswordChangeStatus.Expired,
                Detail: "La contraseña temporal venció. Solicite a administración una nueva.");
        }

        if (!IsStrongPassword(request.NewPassword) ||
            string.Equals(
                request.TemporaryPassword,
                request.NewPassword,
                StringComparison.Ordinal))
        {
            return new TemporaryPasswordChangeResult(
                TemporaryPasswordChangeStatus.InvalidNewPassword,
                Detail:
                    "La nueva contraseña debe ser diferente y contener mayúscula, minúscula, número y carácter especial.");
        }

        var changedAtUtc = DateTime.UtcNow;
        account.PasswordHash = _passwordHasher.HashPassword(
            account,
            request.NewPassword);
        account.RequiereCambioPassword = false;
        account.PasswordTemporalExpiraUtc = null;
        account.PasswordActualizadaUtc = changedAtUtc;
        account.EstadoEnvioPasswordTemporal = "UTILIZADO";
        account.DetalleFalloPasswordTemporal = null;

        if (account.TipoUsuario == "Empleado")
        {
            var employee = await _dbContext.Usuarios.SingleOrDefaultAsync(
                item => item.CodUsuario == account.ReferenciaId,
                cancellationToken);
            if (employee is not null)
            {
                employee.PrimerCambio = false;
            }
        }

        var activeTokens = await _dbContext.RefreshTokens
            .Where(item => item.IdCuenta == account.IdCuenta && !item.Revocado)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revocado = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TemporaryPasswordChangeResult(
            TemporaryPasswordChangeStatus.Success,
            new TemporaryPasswordChangeResponse(
                account.Correo,
                "La contraseña fue cambiada correctamente. Ya puede iniciar sesión.",
                changedAtUtc));
    }

    private static bool IsStrongPassword(string password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= 12 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(character => !char.IsLetterOrDigit(character));
}
