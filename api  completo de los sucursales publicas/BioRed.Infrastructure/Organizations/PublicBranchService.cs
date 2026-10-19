using BioRed.Application.Organizations;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Organizations;

/// <summary>
/// Expone únicamente las sucursales activas que un visitante necesita para
/// completar su registro como cliente.
/// </summary>
public sealed class PublicBranchService : IPublicBranchService
{
    private readonly BioRedDbContext _dbContext;

    public PublicBranchService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetPublicBranchesResponse> GetAsync(
        GetPublicBranchesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query =
            from branch in _dbContext.Sucursales.AsNoTracking()
            join company in _dbContext.Empresas.AsNoTracking()
                on branch.CodEmpresa equals company.CodEmpresa
            where branch.Estado && company.Estado
            select new
            {
                branch.CodSucursal,
                branch.NombreSucursal
            };

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.NombreSucursal)
            .ThenBy(item => item.CodSucursal)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new GetPublicBranchesResponse(
            request.Page,
            request.PageSize,
            totalItems,
            totalPages,
            items
                .Select(item => new PublicBranchResponse(
                    item.CodSucursal,
                    item.NombreSucursal))
                .ToArray());
    }
}
