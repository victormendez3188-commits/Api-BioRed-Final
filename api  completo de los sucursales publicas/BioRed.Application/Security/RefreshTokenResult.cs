namespace BioRed.Application.Security;

public sealed record RefreshTokenResult(
    string Token,
    string TokenHash,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);