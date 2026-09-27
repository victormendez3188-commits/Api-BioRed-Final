using BioRed.Application.Security;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Security;

public sealed class TokenRefreshService : ITokenRefreshService
{
    private readonly BioRedDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAccountIdentityService _accountIdentityService;

    public TokenRefreshService(
        BioRedDbContext dbContext,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        IAccountIdentityService accountIdentityService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _accountIdentityService = accountIdentityService;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        var hash = _refreshTokenService.HashToken(refreshToken.Trim());
        var token = await _dbContext.RefreshTokens.SingleOrDefaultAsync(
            item => item.Token == hash, cancellationToken);
        if (token is null || token.Revocado) return;
        token.Revocado = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TokenRefreshResult> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = ipAddress;

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return InvalidToken();
        }

        var tokenHash = _refreshTokenService.HashToken(request.RefreshToken.Trim());
        var nowUtc = DateTime.UtcNow;

        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            var storedToken = await _dbContext.RefreshTokens
                .Include(item => item.Cuenta)
                .SingleOrDefaultAsync(
                    item => item.Token == tokenHash,
                    cancellationToken);

            if (storedToken is null ||
                storedToken.Revocado ||
                storedToken.FechaExpiracion <= nowUtc ||
                !storedToken.Cuenta.Estado)
            {
                return InvalidToken();
            }

            var authenticatedUser =
                await _accountIdentityService.CreateAuthenticatedUserAsync(
                    storedToken.Cuenta,
                    cancellationToken);

            if (authenticatedUser is null)
            {
                storedToken.Revocado = true;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return InvalidToken();
            }

            storedToken.Revocado = true;

            var newRefreshToken =
                _refreshTokenService.CreateRefreshToken();

            _dbContext.RefreshTokens.Add(new RefreshToken
            {
                IdCuenta = storedToken.IdCuenta,
                Token = newRefreshToken.TokenHash,
                FechaCreacion = newRefreshToken.CreatedAtUtc,
                FechaExpiracion = newRefreshToken.ExpiresAtUtc,
                Revocado = false
            });

            var accessToken =
                _tokenService.CreateAccessToken(authenticatedUser);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new TokenRefreshResult(
                TokenRefreshStatus.Success,
                new TokenRefreshResponse(
                    "Bearer",
                    accessToken.Token,
                    accessToken.ExpiresAtUtc,
                    newRefreshToken.Token,
                    newRefreshToken.ExpiresAtUtc));
        });
    }

    private static TokenRefreshResult InvalidToken() =>
        new(TokenRefreshStatus.InvalidToken);
}
