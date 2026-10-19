using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Security;

public sealed class ChangeTemporaryPasswordRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string TemporaryPassword { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 12)]
    public string NewPassword { get; init; } = string.Empty;
}

public sealed record TemporaryPasswordChangeResponse(
    string Email,
    string Message,
    DateTime ChangedAtUtc);

public enum TemporaryPasswordChangeStatus
{
    Success,
    InvalidCredentials,
    Expired,
    ChangeNotRequired,
    InvalidNewPassword
}

public sealed record TemporaryPasswordChangeResult(
    TemporaryPasswordChangeStatus Status,
    TemporaryPasswordChangeResponse? Response = null,
    string? Detail = null);
