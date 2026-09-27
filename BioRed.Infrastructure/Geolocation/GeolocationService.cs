using BioRed.Application.Geolocation;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BioRed.Infrastructure.Geolocation;

public sealed class GeolocationService : IGeolocationService
{
    private const double EarthRadiusKm = 6371.0088;
    private readonly BioRedDbContext _dbContext;

    public GeolocationService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<NearbyBranchResponse>> GetNearbyBranchesAsync(
        decimal latitude,
        decimal longitude,
        decimal maximumDistanceKm,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        if (maximumDistanceKm <= 0 || maximumDistanceKm > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistanceKm));
        }

        var branches = await
        (
            from branch in _dbContext.Sucursales.AsNoTracking()
            join configuration in _dbContext.ConfiguracionesEntrega.AsNoTracking()
                on branch.CodSucursal equals configuration.CodSucursal
            where
                branch.Estado &&
                configuration.Activo &&
                branch.Latitud != null &&
                branch.Longitud != null
            select new
            {
                branch.CodSucursal,
                branch.NombreSucursal,
                branch.DireccionSucursal,
                Latitude = branch.Latitud!.Value,
                Longitude = branch.Longitud!.Value,
                configuration.RadioMaximoKm,
                configuration.TarifaBase,
                configuration.TarifaPorKmExtra,
                configuration.TiempoEstimadoMin
            }
        ).ToListAsync(cancellationToken);

        return branches
            .Select(item => new
            {
                Branch = item,
                Distance = CalculateDistanceKm(
                    latitude,
                    longitude,
                    item.Latitude,
                    item.Longitude)
            })
            .Where(item =>
                item.Distance <= (double)maximumDistanceKm &&
                item.Distance <= (double)item.Branch.RadioMaximoKm)
            .OrderBy(item => item.Distance)
            .Select(item => new NearbyBranchResponse(
                item.Branch.CodSucursal,
                item.Branch.NombreSucursal,
                item.Branch.DireccionSucursal,
                item.Branch.Latitude,
                item.Branch.Longitude,
                Math.Round(item.Distance, 2),
                item.Branch.RadioMaximoKm,
                item.Branch.TarifaBase,
                item.Branch.TarifaPorKmExtra,
                item.Branch.TiempoEstimadoMin))
            .ToArray();
    }

    public async Task<DriverLocationResponse?> UpdateDriverLocationAsync(
        string driverCode,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
        {
            throw new ArgumentException("La latitud y longitud son obligatorias.", nameof(request));
        }

        var driver = await _dbContext.Repartidores
            .SingleOrDefaultAsync(
                item => item.CodRepartidor == driverCode && item.Estado,
                cancellationToken);

        if (driver is null)
        {
            return null;
        }

        if (request.OrderId.HasValue)
        {
            var ownsOrder = await _dbContext.Pedidos
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.IdPedido == request.OrderId.Value &&
                        item.CodRepartidor == driverCode,
                    cancellationToken);

            if (!ownsOrder)
            {
                return null;
            }
        }

        var receivedAtUtc = DateTime.UtcNow;
        var location = new UbicacionRepartidor
        {
            CodRepartidor = driverCode,
            IdPedido = request.OrderId,
            Latitud = request.Latitude.Value,
            Longitud = request.Longitude.Value,
            PrecisionMetros = request.AccuracyMeters,
            VelocidadKmh = request.SpeedKmh,
            RumboGrados = request.HeadingDegrees,
            FechaDispositivoUtc = request.DeviceRecordedAtUtc,
            FechaRecepcionUtc = receivedAtUtc,
            FechaRegistro = receivedAtUtc
        };

        driver.LatitudActual = request.Latitude.Value;
        driver.LongitudActual = request.Longitude.Value;
        driver.EstadoDisponibilidad = request.OrderId.HasValue
            ? "Ocupado"
            : "Disponible";

        _dbContext.UbicacionesRepartidor.Add(location);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapLocation(location, receivedAtUtc);
    }

    public async Task<TrackingQueryResult> GetOrderTrackingAsync(
        int orderId,
        string userType,
        string referenceId,
        bool canViewAnyOrder,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdPedido == orderId, cancellationToken);

        if (order is null)
        {
            return new TrackingQueryResult(TrackingQueryStatus.NotFound);
        }

        var authorized = canViewAnyOrder ||
            (userType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
             order.CodCliente == referenceId) ||
            (userType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
             order.CodRepartidor == referenceId);

        if (!authorized)
        {
            return new TrackingQueryResult(TrackingQueryStatus.Forbidden);
        }

        var destination = await _dbContext.DireccionesCliente
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdDireccion == order.IdDireccionEntrega,
                cancellationToken);

        if (destination is null)
        {
            return new TrackingQueryResult(TrackingQueryStatus.NotFound);
        }

        var lastLocationEntity = await _dbContext.UbicacionesRepartidor
            .AsNoTracking()
            .Where(item => item.IdPedido == orderId)
            .OrderByDescending(item => item.FechaRegistro)
            .ThenByDescending(item => item.IdUbicacion)
            .FirstOrDefaultAsync(cancellationToken);

        var queriedAtUtc = DateTime.UtcNow;
        var lastLocation = lastLocationEntity is null
            ? null
            : MapLocation(lastLocationEntity, queriedAtUtc);

        var history = await _dbContext.SeguimientosPedido
            .AsNoTracking()
            .Where(item => item.IdPedido == orderId)
            .OrderBy(item => item.FechaHora)
            .Select(item => new OrderStatusItem(
                item.EstadoPedido,
                item.Descripcion,
                item.FechaHora,
                item.ActualizadoPor))
            .ToListAsync(cancellationToken);

        return new TrackingQueryResult(
            TrackingQueryStatus.Success,
            new OrderTrackingResponse(
                order.IdPedido,
                order.CodPedido,
                order.EstadoPedido,
                destination.Latitud,
                destination.Longitud,
                lastLocation,
                history));
    }

    public async Task<OrderPositionQueryResult> GetOrderPositionAsync(
        int orderId,
        string userType,
        string referenceId,
        bool canViewAnyOrder,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdPedido == orderId, cancellationToken);

        if (order is null)
        {
            return new OrderPositionQueryResult(TrackingQueryStatus.NotFound);
        }

        var authorized = canViewAnyOrder ||
            (userType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
             order.CodCliente == referenceId) ||
            (userType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
             order.CodRepartidor == referenceId);

        if (!authorized)
        {
            return new OrderPositionQueryResult(TrackingQueryStatus.Forbidden);
        }

        var branch = await _dbContext.Sucursales
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodSucursal == order.CodSucursal,
                cancellationToken);

        var destination = await _dbContext.DireccionesCliente
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdDireccion == order.IdDireccionEntrega,
                cancellationToken);

        if (branch?.Latitud is null || branch.Longitud is null || destination is null)
        {
            return new OrderPositionQueryResult(TrackingQueryStatus.NotFound);
        }

        Repartidor? driver = null;
        if (!string.IsNullOrWhiteSpace(order.CodRepartidor))
        {
            driver = await _dbContext.Repartidores
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CodRepartidor == order.CodRepartidor,
                    cancellationToken);
        }

        var lastLocationEntity = await _dbContext.UbicacionesRepartidor
            .AsNoTracking()
            .Where(item =>
                item.IdPedido == orderId ||
                (item.IdPedido == null && item.CodRepartidor == order.CodRepartidor))
            .OrderByDescending(item => item.FechaRecepcionUtc)
            .ThenByDescending(item => item.IdUbicacion)
            .FirstOrDefaultAsync(cancellationToken);

        var queriedAtUtc = DateTime.UtcNow;
        var lastLocation = lastLocationEntity is null
            ? null
            : MapLocation(lastLocationEntity, queriedAtUtc);

        var branchDistance = CalculateDistanceKm(
            branch.Latitud.Value,
            branch.Longitud.Value,
            destination.Latitud,
            destination.Longitud);

        double? driverDistance = lastLocation is null
            ? null
            : CalculateDistanceKm(
                lastLocation.Latitude,
                lastLocation.Longitude,
                destination.Latitud,
                destination.Longitud);

        var routeOriginLatitude = lastLocation?.Latitude ?? branch.Latitud.Value;
        var routeOriginLongitude = lastLocation?.Longitude ?? branch.Longitud.Value;

        return new OrderPositionQueryResult(
            TrackingQueryStatus.Success,
            new OrderPositionResponse(
                order.IdPedido,
                order.CodPedido,
                order.EstadoPedido,
                branch.CodSucursal,
                branch.NombreSucursal,
                branch.DireccionSucursal,
                CreatePoint(branch.Latitud.Value, branch.Longitud.Value),
                destination.NombreDireccion,
                destination.DireccionCompleta,
                destination.Referencia,
                CreatePoint(destination.Latitud, destination.Longitud),
                driver?.CodRepartidor,
                driver is null
                    ? null
                    : $"{driver.NombreRepartidor} {driver.ApellidosRepartidor}".Trim(),
                driver?.EstadoDisponibilidad,
                lastLocation,
                Math.Round(branchDistance, 2),
                driverDistance.HasValue ? Math.Round(driverDistance.Value, 2) : null,
                CreateRouteMapUrl(
                    routeOriginLatitude,
                    routeOriginLongitude,
                    destination.Latitud,
                    destination.Longitud),
                queriedAtUtc));
    }

    public async Task<IReadOnlyCollection<ActiveDriverPositionResponse>> GetActiveDriverPositionsAsync(
        string? branchCode,
        CancellationToken cancellationToken = default)
    {
        var drivers = await _dbContext.Repartidores
            .AsNoTracking()
            .Where(item => item.Estado)
            .OrderBy(item => item.NombreRepartidor)
            .ThenBy(item => item.ApellidosRepartidor)
            .ToListAsync(cancellationToken);

        var driverCodes = drivers.Select(item => item.CodRepartidor).ToArray();
        var locationIds = await _dbContext.UbicacionesRepartidor
            .AsNoTracking()
            .Where(item => driverCodes.Contains(item.CodRepartidor))
            .GroupBy(item => item.CodRepartidor)
            .Select(group => group.Max(item => item.IdUbicacion))
            .ToListAsync(cancellationToken);

        var locations = await _dbContext.UbicacionesRepartidor
            .AsNoTracking()
            .Where(item => locationIds.Contains(item.IdUbicacion))
            .ToListAsync(cancellationToken);

        var activeOrders = await _dbContext.Pedidos
            .AsNoTracking()
            .Where(item =>
                item.CodRepartidor != null &&
                item.EstadoPedido != "Entregado" &&
                item.EstadoPedido != "Cancelado")
            .OrderByDescending(item => item.FechaCreacion)
            .ToListAsync(cancellationToken);

        var lastLocations = locations.ToDictionary(item => item.CodRepartidor);
        var ordersByDriver = activeOrders
            .GroupBy(item => item.CodRepartidor!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var queriedAtUtc = DateTime.UtcNow;

        return drivers
            .Select(driver =>
            {
                ordersByDriver.TryGetValue(driver.CodRepartidor, out var order);
                lastLocations.TryGetValue(driver.CodRepartidor, out var location);

                return new { Driver = driver, Order = order, Location = location };
            })
            .Where(item =>
                string.IsNullOrWhiteSpace(branchCode) ||
                string.Equals(
                    item.Order?.CodSucursal,
                    branchCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .Select(item =>
            {
                var responseLocation = item.Location is null
                    ? null
                    : MapLocation(item.Location, queriedAtUtc);

                return new ActiveDriverPositionResponse(
                    item.Driver.CodRepartidor,
                    $"{item.Driver.NombreRepartidor} {item.Driver.ApellidosRepartidor}".Trim(),
                    item.Driver.EstadoDisponibilidad,
                    item.Order?.IdPedido,
                    item.Order?.CodPedido,
                    item.Order?.EstadoPedido,
                    item.Order?.CodSucursal,
                    responseLocation,
                    responseLocation is null
                        ? null
                        : CreatePoint(
                            responseLocation.Latitude,
                            responseLocation.Longitude).MapUrl);
            })
            .ToArray();
    }

    private static DriverLocationResponse MapLocation(
        UbicacionRepartidor location,
        DateTime queriedAtUtc)
    {
        var receivedAtUtc = location.FechaRecepcionUtc == default
            ? location.FechaRegistro
            : location.FechaRecepcionUtc;

        return new DriverLocationResponse(
            location.IdUbicacion,
            location.CodRepartidor,
            location.IdPedido,
            location.Latitud,
            location.Longitud,
            location.FechaRegistro,
            location.PrecisionMetros,
            location.VelocidadKmh,
            location.RumboGrados,
            location.FechaDispositivoUtc,
            receivedAtUtc,
            receivedAtUtc < queriedAtUtc.AddMinutes(-5));
    }

    private static GeographicalPointResponse CreatePoint(decimal latitude, decimal longitude) =>
        new(latitude, longitude, CreateMapUrl(latitude, longitude));

    private static string CreateMapUrl(decimal latitude, decimal longitude) =>
        $"https://www.google.com/maps/search/?api=1&query={FormatCoordinate(latitude)},{FormatCoordinate(longitude)}";

    private static string CreateRouteMapUrl(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude) =>
        "https://www.google.com/maps/dir/?api=1" +
        $"&origin={FormatCoordinate(originLatitude)},{FormatCoordinate(originLongitude)}" +
        $"&destination={FormatCoordinate(destinationLatitude)},{FormatCoordinate(destinationLongitude)}";

    private static string FormatCoordinate(decimal coordinate) =>
        coordinate.ToString("0.########", CultureInfo.InvariantCulture);

    private static double CalculateDistanceKm(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude)
    {
        static double ToRadians(decimal degrees) =>
            (double)degrees * Math.PI / 180d;

        var latitude1 = ToRadians(originLatitude);
        var latitude2 = ToRadians(destinationLatitude);
        var latitudeDelta = ToRadians(destinationLatitude - originLatitude);
        var longitudeDelta = ToRadians(destinationLongitude - originLongitude);

        var haversine =
            Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
            Math.Cos(latitude1) * Math.Cos(latitude2) *
            Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);

        return EarthRadiusKm * 2d * Math.Atan2(
            Math.Sqrt(haversine),
            Math.Sqrt(1d - haversine));
    }
}
