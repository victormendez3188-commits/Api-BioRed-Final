namespace BioRed.Application.Security;

public sealed record CurrentUserResponse(
    long UserId,
    string UserName,
    string? Email,
    string? FullName,
    string? UserType,
    string? ReferenceId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
