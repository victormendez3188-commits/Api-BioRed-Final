using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using BioRed.Application.Routing;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Routing;

public sealed class GoogleRoutesService : IOrderRouteService
{
    private const string FieldMask = "routes.duration,routes.distanceMeters,routes.polyline.encodedPolyline";
    private readonly BioRedDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly GoogleRoutesOptions _options;

    public GoogleRoutesService(
        BioRedDbContext dbContext,
        HttpClient httpClient,
        IOptions<GoogleRoutesOptions> options)
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
        if (originLatitude is < -90 or > 90 ||
            originLongitude is < -180 or > 180 ||
            string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return new(
                OrderRouteStatus.InvalidRequest,
                Detail: "La ubicación de origen no es válida.");
        }

        var normalizedTravelMode = travelMode.Trim().ToUpperInvariant();
        if (normalizedTravelMode is not ("TWO_WHEELER" or "DRIVE" or "BICYCLE" or "WALK"))
        {
            return new(
                OrderRouteStatus.InvalidRequest,
                Detail: "travelMode debe ser TWO_WHEELER, DRIVE, BICYCLE o WALK.");
        }

        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdPedido == orderId, cancellationToken);

        if (order is null)
        {
            return new(OrderRouteStatus.NotFound, Detail: "No se encontró el pedido.");
        }

        if (!actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(order.CodRepartidor, actorReferenceId, StringComparison.OrdinalIgnoreCase))
        {
            return new(OrderRouteStatus.Forbidden, Detail: "Solo el repartidor asignado puede solicitar la ruta.");
        }

        if (!order.EstadoPedido.Equals("En Camino", StringComparison.OrdinalIgnoreCase))
        {
            return new(OrderRouteStatus.InvalidState, Detail: "La ruta solo está disponible cuando el pedido está En Camino.");
        }

        var address = await _dbContext.DireccionesCliente
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdDireccion == order.IdDireccionEntrega, cancellationToken);

        if (address is null ||
            address.Latitud is < -90 or > 90 ||
            address.Longitud is < -180 or > 180)
        {
            return new(OrderRouteStatus.NotFound, Detail: "La dirección del pedido no tiene coordenadas válidas.");
        }

        if (!_options.Enabled)
        {
            return new(OrderRouteStatus.Disabled, Detail: "Google Routes está deshabilitado en la configuración.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new(OrderRouteStatus.Disabled, Detail: "Google Routes está habilitado, pero no tiene API key configurada.");
        }

        var requestBody = new
        {
            origin = new
            {
                location = new
                {
                    latLng = new
                    {
                        latitude = originLatitude,
                        longitude = originLongitude
                    }
                }
            },
            destination = new
            {
                location = new
                {
                    latLng = new
                    {
                        latitude = address.Latitud,
                        longitude = address.Longitud
                    }
                }
            },
            travelMode = normalizedTravelMode,
            routingPreference = "TRAFFIC_AWARE"
        };

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "directions/v2:computeRoutes")
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.TryAddWithoutValidation("X-Goog-Api-Key", _options.ApiKey);
            request.Headers.TryAddWithoutValidation("X-Goog-FieldMask", FieldMask);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new(
                    OrderRouteStatus.Unavailable,
                    Detail: "Google Routes no pudo calcular la ruta.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("routes", out var routes) ||
                routes.GetArrayLength() == 0)
            {
                return new(OrderRouteStatus.Unavailable, Detail: "Google Routes no devolvió rutas.");
            }

            var route = routes[0];
            var distanceMeters = route.TryGetProperty("distanceMeters", out var distance)
                ? distance.GetInt64()
                : 0L;
            var durationSeconds = route.TryGetProperty("duration", out var duration)
                ? ParseDurationSeconds(duration.GetString())
                : 0d;
            var encodedPolyline = route.TryGetProperty("polyline", out var polyline) &&
                                  polyline.TryGetProperty("encodedPolyline", out var encoded)
                ? encoded.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(encodedPolyline))
            {
                return new(OrderRouteStatus.Unavailable, Detail: "Google Routes no devolvió la polilínea de la ruta.");
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(OrderRouteStatus.Unavailable, Detail: "Google Routes tardó demasiado en responder.");
        }
        catch (HttpRequestException)
        {
            return new(OrderRouteStatus.Unavailable, Detail: "No fue posible comunicarse con Google Routes.");
        }
        catch (JsonException)
        {
            return new(OrderRouteStatus.Unavailable, Detail: "Google Routes devolvió una respuesta inválida.");
        }
        catch (InvalidOperationException)
        {
            return new(OrderRouteStatus.Unavailable, Detail: "Google Routes no está configurado correctamente.");
        }
    }

    private static double ParseDurationSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return 0d;
        }

        var normalized = duration.Trim().TrimEnd('s');
        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var seconds)
            ? seconds
            : 0d;
    }
}
