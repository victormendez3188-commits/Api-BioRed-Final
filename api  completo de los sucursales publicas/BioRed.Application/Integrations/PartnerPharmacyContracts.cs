using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Integrations;

public sealed record PartnerProductResponse(
    string ProductCode,
    string ProductName,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl);

public sealed record PartnerProductListResponse(
    string CompanyCode,
    string PartnerName,
    IReadOnlyCollection<PartnerProductResponse> Items);

public sealed record PartnerInventoryResponse(
    string ProductCode,
    int Stock,
    bool Available);

public sealed record PartnerInventoryListResponse(
    string CompanyCode,
    string PartnerName,
    IReadOnlyCollection<PartnerInventoryResponse> Items);

public sealed class CreatePartnerOrderRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Debe agregar al menos un producto.")]
    [MaxLength(100, ErrorMessage = "No puede enviar más de 100 productos diferentes.")]
    public IReadOnlyCollection<CreatePartnerOrderItemRequest> Items { get; init; } =
        Array.Empty<CreatePartnerOrderItemRequest>();
}

public sealed class CreatePartnerOrderItemRequest
{
    [Required]
    [StringLength(50)]
    public string ProductCode { get; init; } = string.Empty;

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

public sealed record PartnerOrderResponse(
    string CompanyCode,
    string PartnerName,
    string OrderCode,
    string Status,
    decimal Total,
    string? Message);

public enum PartnerPharmacyStatus
{
    Success,
    PartnerNotConfigured,
    NotFound,
    InvalidRequest,
    Unavailable,
    InvalidResponse
}

public sealed record PartnerPharmacyResult<T>(
    PartnerPharmacyStatus Status,
    T? Value = default,
    string? Detail = null);

public interface IPartnerPharmacyService
{
    Task<PartnerPharmacyResult<PartnerProductListResponse>> GetProductsAsync(
        string companyCode,
        CancellationToken cancellationToken = default);

    Task<PartnerPharmacyResult<PartnerProductResponse>> GetProductAsync(
        string companyCode,
        string productCode,
        CancellationToken cancellationToken = default);

    Task<PartnerPharmacyResult<PartnerInventoryListResponse>> GetInventoryAsync(
        string companyCode,
        CancellationToken cancellationToken = default);

    Task<PartnerPharmacyResult<PartnerInventoryResponse>> GetInventoryAsync(
        string companyCode,
        string productCode,
        CancellationToken cancellationToken = default);

    Task<PartnerPharmacyResult<PartnerOrderResponse>> CreateOrderAsync(
        string companyCode,
        string clientName,
        CreatePartnerOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PartnerPharmacyResult<PartnerOrderResponse>> GetOrderAsync(
        string companyCode,
        string orderCode,
        CancellationToken cancellationToken = default);
}
