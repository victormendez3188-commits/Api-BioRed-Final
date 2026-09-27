using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Clients;

public sealed class RegisterClientRequest
{
    [Required]
    [StringLength(15)]
    public string BranchCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Document { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; init; }

    [Required]
    [EmailAddress]
    [RegularExpression(@"(?i)^[^\s@]+@gmail\.com$", ErrorMessage = "Use una dirección @gmail.com.")]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

}

public sealed class UpdateClientProfileRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; init; }
}

public sealed record ClientProfileResponse(
    string ClientCode,
    string BranchCode,
    string FirstName,
    string LastName,
    string Document,
    string? Phone,
    string Email,
    bool Active,
    DateTime CreatedAtUtc,
    bool PasswordChangeRequired = false,
    DateTime? TemporaryPasswordExpiresAtUtc = null,
    string? TemporaryPasswordEmailStatus = null);

public enum ClientProfileStatus
{
    Success,
    NotFound,
    BranchNotFound,
    DuplicateEmail,
    DuplicateDocument,
    DuplicateData,
    SystemUserNotFound,
    EmailConfigurationInvalid,
    EmailDeliveryFailed
}

public sealed record ClientProfileResult(
    ClientProfileStatus Status,
    ClientProfileResponse? Profile = null,
    string? Detail = null);

public interface IClientProfileService
{
    Task<ClientProfileResult> RegisterAsync(
        RegisterClientRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientProfileResult> GetAsync(
        string clientCode,
        CancellationToken cancellationToken = default);

    Task<ClientProfileResult> UpdateAsync(
        string clientCode,
        UpdateClientProfileRequest request,
        CancellationToken cancellationToken = default);
}
