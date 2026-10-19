using System.Globalization;
using System.Text.Json;
using BioRed.Application.Routing;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Routing;

/// <summary>
/// Calcula rutas con el servicio público OSRM. La solicitud se construye como
/// una URI absoluta para evitar que HttpClient concatene dos veces la ruta base.
/// </summary>
public sealed class OpenStreetMapRoutingService : IOrderRouteService
{
    private const string DefaultOsrmBaseUrl = "https://router.project-osrm.org";
    private const string OsrmRoutePath = "/route/v1/driving/";

    private readonly BioRedDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly OpenStreetMapRoutingOptions _options;

    public OpenStreetMapRoutingService(
        BioRedDbContext dbContext,
        HttpClient httpClient,
        IOptions<OpenStreetMapRoutingOptions> options)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<OrderRouteResult> GetAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        decimal originLatitude,
        decimal originLongitude,
        string travelMode,
        CancellationToken cancellationToken = default)
    {
        if (originLatitude < -90 || originLatitude > 90 ||
            originLongitude < -180 || originLongitude > 180 ||
            string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return InvalidRequest("La ubicación de origen no es válida.");
        }

        var normalizedTravelMode = (travelMode ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

        if (normalizedTravelMode is not
            ("TWO_WHEELER" or "DRIVE" or "BICYCLE" or "WALK"))
        {
            return InvalidRequest(
                "travelMode debe ser TWO_WHEELER, DRIVE, BICYCLE o WALK.");
        }

        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdPedido == orderId,
                cancellationToken);

        if (order is null)
        {
            return new(OrderRouteStatus.NotFound, Detail: "No se encontró el pedido.");
        }

        if (!string.Equals(actorType, "Repartidor", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(order.CodRepartidor, actorReferenceId, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                OrderRouteStatus.Forbidden,
                Detail: "Solo el repartidor asignado puede solicitar la ruta.");
        }

        if (!string.Equals(order.EstadoPedido, "En Camino", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                OrderRouteStatus.InvalidState,
                Detail: "La ruta solo está disponible cuando el pedido está En Camino.");
        }

        var address = await _dbContext.DireccionesCliente
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdDireccion == order.IdDireccionEntrega,
                cancellationToken);

        if (address is null ||
            address.Latitud < -90 || address.Latitud > 90 ||
            address.Longitud < -180 || address.Longitud > 180)
        {
            return new(
                OrderRouteStatus.NotFound,
                Detail: "La dirección del pedido no tiene coordenadas válidas.");
        }

        if (!_options.Enabled)
        {
            return new(
                OrderRouteStatus.Disabled,
                Detail: "El servicio de mapas está deshabilitado.");
        }

        if (!TryBuildOsrmUri(
                _options.BaseUrl,
                originLatitude,
                originLongitude,
                address.Latitud,
                address.Longitud,
                out var requestUri,
                out var uriError))
        {
            return new(OrderRouteStatus.Unavailable, Detail: uriError);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Accept.ParseAdd("application/json");

            if (!string.IsNullOrWhiteSpace(_options.UserAgent))
            {
                request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new(
                    OrderRouteStatus.Unavailable,
                    Detail: $"OSRM respondió HTTP {(int)response.StatusCode}: {responseBody}");
            }

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            if (!root.TryGetProperty("code", out var code) ||
                !string.Equals(code.GetString(), "Ok", StringComparison.OrdinalIgnoreCase) ||
                !root.TryGetProperty("routes", out var routes) ||
                routes.ValueKind != JsonValueKind.Array ||
                routes.GetArrayLength() == 0)
            {
                return new(
                    OrderRouteStatus.Unavailable,
                    Detail: "OSRM no devolvió ninguna ruta.");
            }

            var route = routes[0];
            long distanceMeters = 0L;
            if (route.TryGetProperty("distance", out var distance))
            {
                if (distance.TryGetInt64(out var integerDistance))
                {
                    distanceMeters = integerDistance;
                }
                else if (distance.TryGetDouble(out var decimalDistance))
                {
                    distanceMeters = (long)Math.Round(
                        decimalDistance,
                        MidpointRounding.AwayFromZero);
                }
            }

            var durationSeconds = route.TryGetProperty("duration", out var duration)
                ? duration.GetDouble()
                : 0D;

            var encodedPolyline = route.TryGetProperty("geometry", out var geometry)
                ? geometry.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(encodedPolyline))
            {
                return new(
                    OrderRouteStatus.Unavailable,
                    Detail: "OSRM no devolvió la polilínea de la ruta.");
            }

            return new(
                OrderRouteStatus.Success,
                new RouteResponse(
                    order.IdPedido,
                    address.Latitud,
                    address.Longitud,
                    address.DireccionCompleta,
                    address.Referencia,
                    distanceMeters,
                    durationSeconds,
                    encodedPolyline));
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new(
                OrderRouteStatus.Unavailable,
                Detail: "El servicio de rutas tardó demasiado en responder.");
        }
        catch (HttpRequestException exception)
        {
            return new(
                OrderRouteStatus.Unavailable,
                Detail: $"No fue posible comunicarse con OSRM: {exception.Message}");
        }
        catch (JsonException)
        {
            return new(
                OrderRouteStatus.Unavailable,
                Detail: "OSRM devolvió una respuesta inválida.");
        }
    }

    private static OrderRouteResult InvalidRequest(string detail) =>
        new(OrderRouteStatus.InvalidRequest, Detail: detail);

    private static bool TryBuildOsrmUri(
        string? configuredBaseUrl,
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude,
        out Uri requestUri,
        out string error)
    {
        requestUri = null!;
        error = string.Empty;

        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? DefaultOsrmBaseUrl
            : configuredBaseUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(baseUri.Host) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            error =
                "OpenStreetMapRouting:BaseUrl debe ser una URL HTTPS base, por ejemplo " +
                "https://router.project-osrm.org/";
            return false;
        }

        var origin = string.Create(
            CultureInfo.InvariantCulture,
            $"{originLongitude:0.######},{originLatitude:0.######}");
        var destination = string.Create(
            CultureInfo.InvariantCulture,
            $"{destinationLongitude:0.######},{destinationLatitude:0.######}");

        // No se usa HttpClient.BaseAddress ni una ruta relativa. Esta cadena es
        // exactamente la URL que acepta el endpoint público de OSRM.
        var requestText =
            $"{baseUri.Scheme}://{baseUri.Authority}" +
            $"{baseUri.AbsolutePath.TrimEnd('/')}{OsrmRoutePath}{origin};{destination}" +
            "?overview=full&geometries=polyline&steps=false";

        if (!Uri.TryCreate(requestText, UriKind.Absolute, out requestUri))
        {
            error = "No se pudo construir la URL absoluta de OSRM.";
            return false;
        }

        return true;
    }
}
