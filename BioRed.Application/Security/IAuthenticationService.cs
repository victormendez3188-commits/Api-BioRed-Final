namespace BioRed.Application.Security;

public interface IAuthenticationService
{
    Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<TemporaryPasswordChangeResult> ChangeTemporaryPasswordAsync(
        ChangeTemporaryPasswordRequest request,
        CancellationToken cancellationToken = default);
}
