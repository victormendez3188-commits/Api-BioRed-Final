namespace BioRed.Application.Security;

public sealed record LoginResponse(
    string TokenType,
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    long UserId,
    string UserName,
    string Email,
    string FullName,
    string UserType,
    string ReferenceId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
