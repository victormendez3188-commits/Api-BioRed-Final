using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BioRed.Application.Integrations;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BioRed.Infrastructure.Integrations;

public sealed class PartnerPharmacyService : IPartnerPharmacyService
{
    public const string HttpClientName = "PartnerPharmacy";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BioRedDbContext _dbContext;
    private readonly ILogger<PartnerPharmacyService> _logger;

    public PartnerPharmacyService(
        IHttpClientFactory httpClientFactory,
        BioRedDbContext dbContext,
        ILogger<PartnerPharmacyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PartnerPharmacyResult<PartnerProductListResponse>> GetProductsAsync(
        string companyCode,
        CancellationToken cancellationToken = default)
    {
        var partnerResult = await ResolvePartnerAsync(companyCode, cancellationToken);
        if (partnerResult.Partner is null)
        {
            return Failure<PartnerProductListResponse>(partnerResult.Detail);
        }

        var response = await SendAsync<List<ExternalProductDto>>(
            partnerResult.Partner,
            HttpMethod.Get,
            "api/Productos",
            null,
            cancellationToken);

        if (response.Status != PartnerPharmacyStatus.Success || response.Value is null)
        {
            return CopyFailure<List<ExternalProductDto>, PartnerProductListResponse>(response);
        }

        var items = response.Value
            .Select(MapProduct)
            .ToArray();

        return new(
            PartnerPharmacyStatus.Success,
            new PartnerProductListResponse(
                partnerResult.Partner.CompanyCode,
                partnerResult.Partner.Name,
                items));
    }

    public async Task<PartnerPharmacyResult<PartnerProductResponse>> GetProductAsync(
        string companyCode,
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var partnerResult = await ResolvePartnerAsync(companyCode, cancellationToken);
        if (partnerResult.Partner is null)
        {
            return Failure<PartnerProductResponse>(partnerResult.Detail);
        }

        var response = await SendAsync<ExternalProductDto>(
            partnerResult.Partner,
            HttpMethod.Get,
            $"api/Productos/{Uri.EscapeDataString(productCode)}",
            null,
            cancellationToken);

        if (response.Status != PartnerPharmacyStatus.Success || response.Value is null)
        {
            return CopyFailure<ExternalProductDto, PartnerProductResponse>(response);
        }

        return new(PartnerPharmacyStatus.Success, MapProduct(response.Value));
    }

    public async Task<PartnerPharmacyResult<PartnerInventoryListResponse>> GetInventoryAsync(
        string companyCode,
        CancellationToken cancellationToken = default)
    {
        var partnerResult = await ResolvePartnerAsync(companyCode, cancellationToken);
        if (partnerResult.Partner is null)
        {
            return Failure<PartnerInventoryListResponse>(partnerResult.Detail);
        }

        var response = await SendAsync<List<ExternalInventoryDto>>(
            partnerResult.Partner,
            HttpMethod.Get,
            "api/Inventario",
            null,
            cancellationToken);

        if (response.Status != PartnerPharmacyStatus.Success || response.Value is null)
        {
            return CopyFailure<List<ExternalInventoryDto>, PartnerInventoryListResponse>(response);
        }

        var items = response.Value
            .Select(MapInventory)
            .ToArray();

        return new(
            PartnerPharmacyStatus.Success,
            new PartnerInventoryListResponse(
                partnerResult.Partner.CompanyCode,
                partnerResult.Partner.Name,
                items));
    }

    public async Task<PartnerPharmacyResult<PartnerInventoryResponse>> GetInventoryAsync(
        string companyCode,
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var partnerResult = await ResolvePartnerAsync(companyCode, cancellationToken);
        if (partnerResult.Partner is null)
        {
            return Failure<PartnerInventoryResponse>(partnerResult.Detail);
        }

        var response = await SendAsync<ExternalInventoryDto>(
            partnerResult.Partner,
            HttpMethod.Get,
            $"api/Inventario/{Uri.EscapeDataString(productCode)}",
            null,
            cancellationToken);

        if (response.Status != PartnerPharmacyStatus.Success || response.Value is null)
        {
            return CopyFailure<ExternalInventoryDto, PartnerInventoryResponse>(response);
        }

        return new(PartnerPharmacyStatus.Success, MapInventory(response.Value));
    }

    public async Task<PartnerPharmacyResult<PartnerOrderResponse>> CreateOrderAsync(
        string companyCode,
        string clientName,
        CreatePartnerOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var partnerResult = await ResolvePartnerAsync(companyCode, cancellationToken);
        if (partnerResult.Partner is null)
        {
            return Failure<PartnerOrderResponse>(partnerResult.Detail);
        }

        var externalRequest = new ExternalOrderRequestDto(
            new ExternalClientDto(clientName),
            request.Items
                .Select(item => new ExternalOrderItemDto(item.ProductCode, item.Quantity))
                .ToArray());

        var response = await SendAsync<ExternalOrderResponseDto>(
            partnerResult.Partner,
            HttpMethod.Post,
            "api/Pedidos",
            externalRequest,
            cancellationToken);

        if (response.Status != PartnerPharmacyStatus.Success || response.Value is null)
        {
            return CopyFailure<ExternalOrderResponseDto, PartnerOrderResponse>(response);
        }

        return MapOrder(partnerResult.Partner, response.Value);
    }

    public async Task<PartnerPharmacyResult<PartnerOrderResponse>> GetOrderAsync(
        string companyCode,
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        var partnerResult = await ResolvePartnerAsync(companyCode, cancellationToken);
        if (partnerResult.Partner is null)
        {
            return Failure<PartnerOrderResponse>(partnerResult.Detail);
        }

        var response = await SendAsync<ExternalOrderResponseDto>(
            partnerResult.Partner,
            HttpMethod.Get,
            $"api/Pedidos/{Uri.EscapeDataString(orderCode)}",
            null,
            cancellationToken);

        if (response.Status != PartnerPharmacyStatus.Success || response.Value is null)
        {
            return CopyFailure<ExternalOrderResponseDto, PartnerOrderResponse>(response);
        }

        return MapOrder(partnerResult.Partner, response.Value);
    }

    private async Task<PartnerPharmacyResult<T>> SendAsync<T>(
        PartnerDescriptor partner,
        HttpMethod method,
        string relativePath,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                method,
                new Uri(CreateBaseUri(partner.BaseUrl), relativePath));

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new(
                    PartnerPharmacyStatus.NotFound,
                    Detail: "La farmacia asociada no encontró el recurso solicitado.");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new(
                    PartnerPharmacyStatus.InvalidRequest,
                    Detail: "La farmacia asociada rechazó los datos enviados.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "La integración {CompanyCode} respondió HTTP {StatusCode}.",
                    partner.CompanyCode,
                    (int)response.StatusCode);

                return new(
                    PartnerPharmacyStatus.Unavailable,
                    Detail: "La farmacia asociada no pudo completar la solicitud.");
            }

            var value = await response.Content.ReadFromJsonAsync<T>(
                JsonOptions,
                cancellationToken);

            return value is null
                ? new(
                    PartnerPharmacyStatus.InvalidResponse,
                    Detail: "La farmacia asociada devolvió una respuesta vacía.")
                : new(PartnerPharmacyStatus.Success, value);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "Tiempo de espera agotado al consultar {CompanyCode}.",
                partner.CompanyCode);

            return new(
                PartnerPharmacyStatus.Unavailable,
                Detail: "La farmacia asociada tardó demasiado en responder.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "No fue posible conectar con {CompanyCode}.",
                partner.CompanyCode);

            return new(
                PartnerPharmacyStatus.Unavailable,
                Detail: "No fue posible conectar con la farmacia asociada.");
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Respuesta JSON inválida de {CompanyCode}.",
                partner.CompanyCode);

            return new(
                PartnerPharmacyStatus.InvalidResponse,
                Detail: "La farmacia asociada devolvió datos con un formato inesperado.");
        }
    }

    private async Task<(PartnerDescriptor? Partner, string? Detail)> ResolvePartnerAsync(
        string companyCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = companyCode.Trim().ToUpperInvariant();

        var association = await _dbContext.IntegracionesFarmacia
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.CodEmpresa == normalizedCode &&
                    item.EstadoIntegracion == "Activa" &&
                    item.ConexionValidada,
                cancellationToken);

        return association is null
            ? (null, $"No existe una integración activa para la empresa {companyCode}.")
            : (new PartnerDescriptor(
                association.CodEmpresa,
                association.NombreFarmacia,
                association.UrlBaseApi), null);
    }

    private static Uri CreateBaseUri(string baseUrl) =>
        new(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute);

    private static PartnerProductResponse MapProduct(ExternalProductDto value) =>
        new(
            value.Codigo ?? string.Empty,
            value.Nombre ?? string.Empty,
            value.Descripcion,
            value.Precio,
            value.Stock,
            value.ImagenUrl);

    private static PartnerInventoryResponse MapInventory(ExternalInventoryDto value) =>
        new(
            value.Codigo ?? string.Empty,
            value.Stock,
            value.Disponible);

    private static PartnerPharmacyResult<PartnerOrderResponse> MapOrder(
        PartnerDescriptor partner,
        ExternalOrderResponseDto value)
    {
        if (string.IsNullOrWhiteSpace(value.CodigoPedido) ||
            string.IsNullOrWhiteSpace(value.Estado))
        {
            return new(
                PartnerPharmacyStatus.InvalidResponse,
                Detail: "La respuesta de la farmacia no incluye el código o el estado del pedido.");
        }

        return new(
            PartnerPharmacyStatus.Success,
            new PartnerOrderResponse(
                partner.CompanyCode,
                partner.Name,
                value.CodigoPedido,
                value.Estado,
                value.Total,
                value.Mensaje));
    }

    private static PartnerPharmacyResult<T> Failure<T>(string? detail) =>
        new(PartnerPharmacyStatus.PartnerNotConfigured, Detail: detail);

    private static PartnerPharmacyResult<TTarget> CopyFailure<TSource, TTarget>(
        PartnerPharmacyResult<TSource> source) =>
        new(source.Status, Detail: source.Detail);

    private sealed record ExternalProductDto(
        string? Codigo,
        string? Nombre,
        string? Descripcion,
        decimal Precio,
        int Stock,
        string? ImagenUrl);

    private sealed record ExternalInventoryDto(
        string? Codigo,
        int Stock,
        bool Disponible);

    private sealed record ExternalClientDto(string Nombre);

    private sealed record ExternalOrderItemDto(string Codigo, int Cantidad);

    private sealed record ExternalOrderRequestDto(
        ExternalClientDto Cliente,
        IReadOnlyCollection<ExternalOrderItemDto> Productos);

    private sealed record ExternalOrderResponseDto(
        string? CodigoPedido,
        string? Estado,
        decimal Total,
        string? Mensaje);

    private sealed record PartnerDescriptor(
        string CompanyCode,
        string Name,
        string BaseUrl);
}
