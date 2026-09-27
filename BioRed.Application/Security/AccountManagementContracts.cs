using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Security;

public sealed class CreateAccountRequest
{
    [Required]
    [EmailAddress]
    [RegularExpression(@"(?i)^[^\s@]+@gmail\.com$", ErrorMessage = "Use una dirección @gmail.com.")]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(
        "^(Empleado|Cliente|Repartidor)$",
        ErrorMessage = "El tipo debe ser Empleado, Cliente o Repartidor.")]
    public string UserType { get; init; } = string.Empty;

    [Required]
    [StringLength(15)]
    public string ReferenceId { get; init; } = string.Empty;
}

public sealed record AccountResponse(
    int AccountId,
    string Email,
    string UserType,
    string ReferenceId,
    bool Active,
    DateTime CreatedAtUtc,
    bool PasswordChangeRequired,
    DateTime? TemporaryPasswordExpiresAtUtc,
    string TemporaryPasswordEmailStatus);

public enum CreateAccountStatus
{
    Success,
    Duplicate,
    ReferenceNotFound,
    EmailConfigurationInvalid,
    EmailDeliveryFailed,
    NotFound
}

public sealed record CreateAccountResult(
    CreateAccountStatus Status,
    AccountResponse? Account = null);

public interface IAccountManagementService
{
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);
    Task<CreateAccountResult> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateAccountResult> ReissueTemporaryPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default);
}
