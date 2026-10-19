using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Drivers;

public sealed class RegisterDriverRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Document { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [RegularExpression(@"(?i)^[^\s@]+@gmail\.com$", ErrorMessage = "Use una dirección @gmail.com.")]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string VehicleType { get; init; } = string.Empty;

    [StringLength(20)]
    public string? LicensePlate { get; init; }
}

/// <summary>
/// Contract used by the mobile self-registration screen. It deliberately uses
/// a different route from the administrator/temporary-password flow so both
/// onboarding methods can coexist.
/// </summary>
public sealed class RegisterDriverFromMobileRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, EmailAddress]
    [RegularExpression(@"(?i)^[^\s@]+@gmail\.com$", ErrorMessage = "Use una dirección @gmail.com.")]
    [StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9+\-\s]{8,20}$", ErrorMessage = "El teléfono no tiene un formato válido.")]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{13,20}$", ErrorMessage = "El DPI debe contener entre 13 y 20 dígitos.")]
    [StringLength(20)]
    public string DocumentNumber { get; init; } = string.Empty;

    [Required, StringLength(30)]
    public string LicenseNumber { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string VehicleType { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Plate { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "La confirmación no coincide con la contraseña.")]
    [StringLength(200, MinimumLength = 12)]
    public string ConfirmPassword { get; init; } = string.Empty;
}

public sealed class UpdateDriverProfileRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Phone { get; init; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string VehicleType { get; init; } = string.Empty;

    [StringLength(20)]
    public string? LicensePlate { get; init; }

    [StringLength(30)]
    public string? LicenseNumber { get; init; }
}

public sealed class UpdateDriverAvailabilityRequest
{
    [Required]
    [RegularExpression(
        "^(Disponible|Desconectado)$",
        ErrorMessage = "La disponibilidad debe ser Disponible o Desconectado.")]
    public string Availability { get; init; } = string.Empty;
}

public sealed record DriverProfileResponse(
    string DriverCode,
    string FirstName,
    string LastName,
    string Document,
    string Phone,
    string Email,
    string VehicleType,
    string? LicensePlate,
    string Availability,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    bool Active,
    DateTime CreatedAtUtc,
    bool PasswordChangeRequired = false,
    DateTime? TemporaryPasswordExpiresAtUtc = null,
    string? TemporaryPasswordEmailStatus = null,
    string? LicenseNumber = null);

public enum DriverProfileStatus
{
    Success,
    NotFound,
    DuplicateEmail,
    DuplicateDocument,
    DuplicateLicensePlate,
    DuplicateLicenseNumber,
    DuplicateData,
    Busy,
    InvalidPassword,
    EmailConfigurationInvalid,
    EmailDeliveryFailed
}

public sealed record DriverProfileResult(
    DriverProfileStatus Status,
    DriverProfileResponse? Profile = null,
    string? Detail = null);

public interface IDriverProfileService
{
    Task<DriverProfileResult> RegisterAsync(
        RegisterDriverRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverProfileResult> RegisterFromMobileAsync(
        RegisterDriverFromMobileRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverProfileResult> GetAsync(
        string driverCode,
        CancellationToken cancellationToken = default);

    Task<DriverProfileResult> UpdateAsync(
        string driverCode,
        UpdateDriverProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverProfileResult> UpdateAvailabilityAsync(
        string driverCode,
        UpdateDriverAvailabilityRequest request,
        CancellationToken cancellationToken = default);
}
