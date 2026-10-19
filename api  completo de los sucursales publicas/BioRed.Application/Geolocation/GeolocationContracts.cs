using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Geolocation;

public sealed class UpdateDriverLocationRequest : IValidatableObject
{
    [Required(ErrorMessage = "La latitud es obligatoria.")]
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public decimal? Latitude { get; init; }

    [Required(ErrorMessage = "La longitud es obligatoria.")]
    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public decimal? Longitude { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "El pedido no es válido.")]
    public int? OrderId { get; init; }

    [Range(0, 10_000, ErrorMessage = "La precisión debe estar entre 0 y 10000 metros.")]
    public decimal? AccuracyMeters { get; init; }

    [Range(0, 400, ErrorMessage = "La velocidad debe estar entre 0 y 400 km/h.")]
    public decimal? SpeedKmh { get; init; }

    [Range(0, 359.99, ErrorMessage = "El rumbo debe estar entre 0 y 359.99 grados.")]
    public decimal? HeadingDegrees { get; init; }

    public DateTime? DeviceRecordedAtUtc { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeviceRecordedAtUtc.HasValue &&
            DeviceRecordedAtUtc.Value > DateTime.UtcNow.AddMinutes(5))
        {
            yield return new ValidationResult(
                "La fecha del dispositivo no puede estar más de 5 minutos en el futuro.",
                new[] { nameof(DeviceRecordedAtUtc) });
        }
    }
}

public sealed record DriverLocationResponse(
    long LocationId,
    string DriverCode,
    int? OrderId,
    decimal Latitude,
    decimal Longitude,
    DateTime RegisteredAtUtc,
    decimal? AccuracyMeters,
    decimal? SpeedKmh,
    decimal? HeadingDegrees,
    DateTime? DeviceRecordedAtUtc,
    DateTime ReceivedAtUtc,
    bool IsStale);

public sealed record NearbyBranchResponse(
    string BranchCode,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    double DistanceKm,
    decimal DeliveryRadiusKm,
    decimal BaseRate,
    decimal ExtraKmRate,
    int EstimatedMinutes);

public sealed record OrderStatusItem(
    string Status,
    string? Description,
    DateTime RegisteredAtUtc,
    string? UpdatedBy);

public sealed record OrderTrackingResponse(
    int OrderId,
    string OrderCode,
    string CurrentStatus,
    decimal DestinationLatitude,
    decimal DestinationLongitude,
    DriverLocationResponse? LastDriverLocation,
    IReadOnlyCollection<OrderStatusItem> History);

public sealed record GeographicalPointResponse(
    decimal Latitude,
    decimal Longitude,
    string MapUrl);

public sealed record OrderPositionResponse(
    int OrderId,
    string OrderCode,
    string OrderStatus,
    string BranchCode,
    string BranchName,
    string BranchAddress,
    GeographicalPointResponse BranchLocation,
    string DestinationName,
    string DestinationAddress,
    string? DestinationReference,
    GeographicalPointResponse DestinationLocation,
    string? DriverCode,
    string? DriverName,
    string? DriverAvailability,
    DriverLocationResponse? LastDriverLocation,
    double BranchToDestinationKm,
    double? DriverToDestinationKm,
    string RouteMapUrl,
    DateTime QueriedAtUtc);

public sealed record ActiveDriverPositionResponse(
    string DriverCode,
    string DriverName,
    string Availability,
    int? OrderId,
    string? OrderCode,
    string? OrderStatus,
    string? BranchCode,
    DriverLocationResponse? LastLocation,
    string? MapUrl);

public enum TrackingQueryStatus
{
    Success,
    NotFound,
    Forbidden
}

public sealed record TrackingQueryResult(
    TrackingQueryStatus Status,
    OrderTrackingResponse? Tracking = null);

public sealed record OrderPositionQueryResult(
    TrackingQueryStatus Status,
    OrderPositionResponse? Position = null);

public interface IGeolocationService
{
    Task<IReadOnlyCollection<NearbyBranchResponse>> GetNearbyBranchesAsync(
        decimal latitude,
        decimal longitude,
        decimal maximumDistanceKm,
        CancellationToken cancellationToken = default);

    Task<DriverLocationResponse?> UpdateDriverLocationAsync(
        string driverCode,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default);

    Task<TrackingQueryResult> GetOrderTrackingAsync(
        int orderId,
        string userType,
        string referenceId,
        bool canViewAnyOrder,
        CancellationToken cancellationToken = default);

    Task<OrderPositionQueryResult> GetOrderPositionAsync(
        int orderId,
        string userType,
        string referenceId,
        bool canViewAnyOrder,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ActiveDriverPositionResponse>> GetActiveDriverPositionsAsync(
        string? branchCode,
        CancellationToken cancellationToken = default);
}
