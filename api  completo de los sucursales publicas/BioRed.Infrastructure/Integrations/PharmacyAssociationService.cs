using System.Net;
using System.Text.Json;
using BioRed.Application.Integrations;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BioRed.Infrastructure.Integrations;

public sealed class PharmacyAssociationService : IPharmacyAssociationService
{
    private readonly BioRedDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PharmacyAssociationService> _logger;

    public PharmacyAssociationService(
        BioRedDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<PharmacyAssociationService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PharmacyAssociationResult> CreateAsync(
        CreatePharmacyAssociationRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyCode = request.CompanyCode.Trim().ToUpperInvariant();

        if (!TryNormalizeApiUrl(request.ApiBaseUrl, out var apiBaseUrl))
        {
            return new(
                PharmacyAssociationStatus.InvalidUrl,
                Detail: "La URL debe ser HTTPS, pública y no puede incluir /swagger/index.html.");
        }

        var alreadyExists = await _dbContext.IntegracionesFarmacia
            .AnyAsync(item => item.CodEmpresa == companyCode, cancellationToken);

        if (alreadyExists)
        {
            return new(
                PharmacyAssociationStatus.Duplicate,
                Detail: $"Ya existe una solicitud para la empresa {companyCode}.");
        }

        var association = new IntegracionFarmacia
        {
            CodEmpresa = companyCode,
            NombreFarmacia = request.PharmacyName.Trim(),
            UrlBaseApi = apiBaseUrl,
            NombreContacto = request.ContactName.Trim(),
            CorreoContacto = request.ContactEmail.Trim().ToLowerInvariant(),
            EstadoIntegracion = "Pendiente",
            ConexionValidada = false,
            FechaSolicitud = DateTime.UtcNow
        };

        _dbContext.IntegracionesFarmacia.Add(association);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            PharmacyAssociationStatus.Success,
            Map(association));
    }

    public async Task<IReadOnlyCollection<PharmacyAssociationResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var associations = await _dbContext.IntegracionesFarmacia
            .AsNoTracking()
            .OrderByDescending(item => item.FechaSolicitud)
            .ToArrayAsync(cancellationToken);

        return associations.Select(Map).ToArray();
    }

    public async Task<PharmacyAssociationResult> GetByIdAsync(
        int associationId,
        CancellationToken cancellationToken = default)
    {
        var association = await _dbContext.IntegracionesFarmacia
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdIntegracion == associationId,
                cancellationToken);

        return association is null
            ? NotFound(associationId)
            : new(PharmacyAssociationStatus.Success, Map(association));
    }

    public async Task<PharmacyAssociationResult> ValidateConnectionAsync(
        int associationId,
        string actorReferenceId,
        CancellationToken cancellationToken = default)
    {
        var association = await FindAsync(associationId, cancellationToken);
        if (association is null)
        {
            return NotFound(associationId);
        }

        if (association.EstadoIntegracion == "Activa")
        {
            return new(
                PharmacyAssociationStatus.InvalidState,
                Map(association),
                "La integración ya está activa.");
        }

        var connectionSucceeded = await ValidateEndpointsAsync(
            association,
            cancellationToken);

        association.ConexionValidada = connectionSucceeded;
        association.FechaUltimaValidacion = DateTime.UtcNow;
        association.ActualizadoPor = actorReferenceId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return connectionSucceeded
            ? new(PharmacyAssociationStatus.Success, Map(association))
            : new(
                PharmacyAssociationStatus.ConnectionFailed,
                Map(association),
                "No fue posible consultar productos e inventario mediante la API de la farmacia.");
    }

    public async Task<PharmacyAssociationResult> ApproveAsync(
        int associationId,
        string actorReferenceId,
        CancellationToken cancellationToken = default)
    {
        var association = await FindAsync(associationId, cancellationToken);
        if (association is null)
        {
            return NotFound(associationId);
        }

        if (association.EstadoIntegracion != "Pendiente" ||
            !association.ConexionValidada)
        {
            return new(
                PharmacyAssociationStatus.InvalidState,
                Map(association),
                "Primero debe validar correctamente la conexión de una solicitud pendiente.");
        }

        association.EstadoIntegracion = "Activa";
        association.FechaAprobacion = DateTime.UtcNow;
        association.ActualizadoPor = actorReferenceId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(PharmacyAssociationStatus.Success, Map(association));
    }

    public async Task<PharmacyAssociationResult> SuspendAsync(
        int associationId,
        string actorReferenceId,
        CancellationToken cancellationToken = default)
    {
        var association = await FindAsync(associationId, cancellationToken);
        if (association is null)
        {
            return NotFound(associationId);
        }

        if (association.EstadoIntegracion != "Activa")
        {
            return new(
                PharmacyAssociationStatus.InvalidState,
                Map(association),
                "Solo se puede suspender una integración activa.");
        }

        association.EstadoIntegracion = "Suspendida";
        association.ActualizadoPor = actorReferenceId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(PharmacyAssociationStatus.Success, Map(association));
    }

    private async Task<bool> ValidateEndpointsAsync(
        IntegracionFarmacia association,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(
                PartnerPharmacyService.HttpClientName);
            var baseUri = new Uri(association.UrlBaseApi, UriKind.Absolute);

            using var productsResponse = await client.GetAsync(
                new Uri(baseUri, "api/Productos"),
                cancellationToken);

            using var inventoryResponse = await client.GetAsync(
                new Uri(baseUri, "api/Inventario"),
                cancellationToken);

            if (!productsResponse.IsSuccessStatusCode ||
                !inventoryResponse.IsSuccessStatusCode)
            {
                return false;
            }

            await using var productsStream = await productsResponse.Content
                .ReadAsStreamAsync(cancellationToken);
            await using var inventoryStream = await inventoryResponse.Content
                .ReadAsStreamAsync(cancellationToken);

            using var productsJson = await JsonDocument.ParseAsync(
                productsStream,
                cancellationToken: cancellationToken);
            using var inventoryJson = await JsonDocument.ParseAsync(
                inventoryStream,
                cancellationToken: cancellationToken);

            return productsJson.RootElement.ValueKind == JsonValueKind.Array &&
                   inventoryJson.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or
            TaskCanceledException or
            JsonException)
        {
            _logger.LogWarning(
                exception,
                "Falló la validación de la integración {AssociationId}.",
                association.IdIntegracion);

            return false;
        }
    }

    private Task<IntegracionFarmacia?> FindAsync(
        int associationId,
        CancellationToken cancellationToken) =>
        _dbContext.IntegracionesFarmacia.SingleOrDefaultAsync(
            item => item.IdIntegracion == associationId,
            cancellationToken);

    private static bool TryNormalizeApiUrl(string value, out string normalized)
    {
        normalized = string.Empty;

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.IsLoopback ||
            IPAddress.TryParse(uri.Host, out _))
        {
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        };

        normalized = builder.Uri.AbsoluteUri;
        return true;
    }

    private static PharmacyAssociationResult NotFound(int associationId) =>
        new(
            PharmacyAssociationStatus.NotFound,
            Detail: $"No existe la solicitud de asociación {associationId}.");

    private static PharmacyAssociationResponse Map(IntegracionFarmacia value) =>
        new(
            value.IdIntegracion,
            value.CodEmpresa,
            value.NombreFarmacia,
            value.UrlBaseApi,
            value.NombreContacto,
            value.CorreoContacto,
            value.EstadoIntegracion,
            value.ConexionValidada,
            value.FechaSolicitud,
            value.FechaUltimaValidacion,
            value.FechaAprobacion);
}
