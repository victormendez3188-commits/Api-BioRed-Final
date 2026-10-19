using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Notifications;

public sealed class RegisterNotificationDeviceRequest
{
    [Required]
    [StringLength(450, MinimumLength = 10)]
    public string Token { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(Android|iOS)$", ErrorMessage = "La plataforma debe ser Android o iOS.")]
    public string Platform { get; init; } = string.Empty;

    [StringLength(120)]
    public string? DeviceName { get; init; }
}

public sealed record NotificationDeviceResponse(
    long DeviceId,
    string Platform,
    string? DeviceName,
    bool Active,
    DateTime RegisteredAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastConnectionUtc);

public sealed class UnregisterNotificationDeviceRequest
{
    [Required]
    [StringLength(450, MinimumLength = 10)]
    public string Token { get; init; } = string.Empty;
}

public interface IDeviceNotificationService
{
    Task<NotificationDeviceResponse> RegisterAsync(
        int accountId,
        RegisterNotificationDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task UnregisterAsync(
        int accountId,
        string token,
        CancellationToken cancellationToken = default);
}
