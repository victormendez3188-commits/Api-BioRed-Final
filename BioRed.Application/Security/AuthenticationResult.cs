namespace BioRed.Application.Security;

public sealed record AuthenticationResult(
    AuthenticationStatus Status,
    LoginResponse? Response = null,
    DateTime? LockedUntilUtc = null)
{
    public bool Succeeded =>
        Status == AuthenticationStatus.Success &&
        Response is not null;
}