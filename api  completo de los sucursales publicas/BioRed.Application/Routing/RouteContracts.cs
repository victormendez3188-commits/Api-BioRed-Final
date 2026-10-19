using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Routing;

public sealed record RouteResponse(
    int OrderId,
    decimal DestinationLatitude,
    decimal DestinationLongitude,
    string DeliveryAddress,
    string? DeliveryReference,
    long DistanceMeters,
    double DurationSeconds,
    string EncodedPolyline);

public enum OrderRouteStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidRequest,
    InvalidState,
    Disabled,
    Unavailable
}

public sealed record OrderRouteResult(
    OrderRouteStatus Status,
    RouteResponse? Route = null,
    string? Detail = null);

public interface IOrderRouteService
{
    Task<OrderRouteResult> GetAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        decimal originLatitude,
        decimal originLongitude,
        string travelMode,
        CancellationToken cancellationToken = default);
}
