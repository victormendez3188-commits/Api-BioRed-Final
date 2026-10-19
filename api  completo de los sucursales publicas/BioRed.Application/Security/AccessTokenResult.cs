namespace BioRed.Application.Security;

public sealed record AccessTokenResult(
    string Token,
    DateTime ExpiresAtUtc);