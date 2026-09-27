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
    string? TemporaryPasswordEmailStatus = null);

public enum DriverProfileStatus
{
    Success,
    NotFound,
    DuplicateEmail,
    DuplicateDocument,
    DuplicateLicensePlate,
    DuplicateData,
    Busy,
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
