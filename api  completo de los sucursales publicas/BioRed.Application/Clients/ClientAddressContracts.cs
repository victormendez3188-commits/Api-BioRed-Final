using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Clients;

public sealed class SaveClientAddressRequest
{
    [Required]
    [StringLength(50)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string FullAddress { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Reference { get; init; }

    [Range(-90, 90)]
    public decimal Latitude { get; init; }

    [Range(-180, 180)]
    public decimal Longitude { get; init; }

    public bool IsDefault { get; init; }
}

public sealed record ClientAddressResponse(
    int AddressId,
    string Name,
    string FullAddress,
    string? Reference,
    decimal Latitude,
    decimal Longitude,
    bool IsDefault,
    bool Active);

public enum ClientAddressStatus
{
    Success,
    ClientNotFound,
    AddressNotFound
}

public sealed record GetClientAddressesResult(
    ClientAddressStatus Status,
    IReadOnlyCollection<ClientAddressResponse>? Addresses = null,
    string? Detail = null);

public sealed record ClientAddressResult(
    ClientAddressStatus Status,
    ClientAddressResponse? Address = null,
    string? Detail = null);

public interface IClientAddressService
{
    Task<GetClientAddressesResult> GetAsync(
        string clientCode,
        CancellationToken cancellationToken = default);

    Task<ClientAddressResult> GetByIdAsync(
        string clientCode,
        int addressId,
        CancellationToken cancellationToken = default);

    Task<ClientAddressResult> CreateAsync(
        string clientCode,
        SaveClientAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientAddressResult> UpdateAsync(
        string clientCode,
        int addressId,
        SaveClientAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientAddressResult> SetDefaultAsync(
        string clientCode,
        int addressId,
        CancellationToken cancellationToken = default);

    Task<ClientAddressStatus> DeactivateAsync(
        string clientCode,
        int addressId,
        CancellationToken cancellationToken = default);
}
