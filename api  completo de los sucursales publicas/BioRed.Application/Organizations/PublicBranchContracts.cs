using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Organizations;

/// <summary>
/// Parámetros para consultar las sucursales disponibles durante el registro.
/// Esta consulta no requiere una sesión autenticada.
/// </summary>
public sealed class GetPublicBranchesRequest
{
    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 100;
}

/// <summary>
/// Información mínima y pública de una sucursal. No incluye correo,
/// coordenadas, tarifas ni configuración interna de entregas.
/// </summary>
public sealed record PublicBranchResponse(
    string BranchCode,
    string BranchName);

public sealed record GetPublicBranchesResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<PublicBranchResponse> Items);

public interface IPublicBranchService
{
    Task<GetPublicBranchesResponse> GetAsync(
        GetPublicBranchesRequest request,
        CancellationToken cancellationToken = default);
}
