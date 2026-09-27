namespace BioRed.Application.Security;

public interface ITokenRefreshService
{
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<TokenRefreshResult> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
