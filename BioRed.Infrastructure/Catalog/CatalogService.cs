using BioRed.Application.Catalog;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Catalog;

public sealed class CatalogService : ICatalogService
{
    private readonly BioRedDbContext _dbContext;

    public CatalogService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetCatalogProductsResult> GetProductsAsync(
        string branchCode,
        GetCatalogProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedBranchCode = branchCode.Trim();
        var branch = await _dbContext.Sucursales
            .AsNoTracking()
            .Where(item =>
                item.CodSucursal == normalizedBranchCode &&
                item.Estado)
            .Select(item => new
            {
                item.CodSucursal,
                item.CodEmpresa,
                item.NombreSucursal
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (branch is null)
        {
            return new GetCatalogProductsResult(
                CatalogQueryStatus.BranchNotFound,
                Detail: "No se encontró una sucursal activa con ese código.");
        }

        var query =
            from inventory in _dbContext.Inventarios.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on inventory.CodProducto equals product.CodProducto
            where inventory.CodSucursal == branch.CodSucursal &&
                  product.CodEmpresa == branch.CodEmpresa &&
                  product.Estado
            select new
            {
                product.CodProducto,
                product.NombreProducto,
                product.DescripcionProducto,
                product.PrecioVenta,
                product.RequiereReceta,
                inventory.CantidadActual
            };

        if (request.OnlyAvailable)
        {
            query = query.Where(item => item.CantidadActual > 0);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.CodProducto.Contains(search) ||
                item.NombreProducto.Contains(search) ||
                (item.DescripcionProducto != null &&
                 item.DescripcionProducto.Contains(search)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / request.PageSize);
        var rowsToSkip = (request.Page - 1) * request.PageSize;

        var products = await query
            .OrderBy(item => item.NombreProducto)
            .ThenBy(item => item.CodProducto)
            .Skip(rowsToSkip)
            .Take(request.PageSize)
            .Select(item => new CatalogProductResponse(
                item.CodProducto,
                item.NombreProducto,
                item.DescripcionProducto,
                item.PrecioVenta,
                item.CantidadActual,
                item.CantidadActual > 0,
                item.RequiereReceta))
            .ToListAsync(cancellationToken);

        return new GetCatalogProductsResult(
            CatalogQueryStatus.Success,
            new GetCatalogProductsResponse(
                branch.CodSucursal,
                branch.NombreSucursal,
                request.Page,
                request.PageSize,
                totalItems,
                totalPages,
                products));
    }

    public async Task<GetCatalogProductDetailResult> GetProductByCodeAsync(
        string branchCode,
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchCode = branchCode.Trim();
        var normalizedProductCode = productCode.Trim();

        var branch = await _dbContext.Sucursales
            .AsNoTracking()
            .Where(item =>
                item.CodSucursal == normalizedBranchCode &&
                item.Estado)
            .Select(item => new
            {
                item.CodSucursal,
                item.CodEmpresa,
                item.NombreSucursal
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (branch is null)
        {
            return new GetCatalogProductDetailResult(
                CatalogQueryStatus.BranchNotFound,
                Detail: "No se encontró una sucursal activa con ese código.");
        }

        var product = await
        (
            from inventory in _dbContext.Inventarios.AsNoTracking()
            join item in _dbContext.Productos.AsNoTracking()
                on inventory.CodProducto equals item.CodProducto
            where inventory.CodSucursal == branch.CodSucursal &&
                  item.CodProducto == normalizedProductCode &&
                  item.CodEmpresa == branch.CodEmpresa &&
                  item.Estado
            select new CatalogProductDetailResponse(
                branch.CodSucursal,
                branch.NombreSucursal,
                item.CodProducto,
                item.NombreProducto,
                item.DescripcionProducto,
                item.PrecioVenta,
                inventory.CantidadActual,
                inventory.StockMinimo,
                inventory.StockMaximo,
                inventory.CantidadActual > 0,
                inventory.UltimaActualizacion,
                item.RequiereReceta)
        )
        .SingleOrDefaultAsync(cancellationToken);

        return product is null
            ? new GetCatalogProductDetailResult(
                CatalogQueryStatus.ProductNotFound,
                Detail: "El producto no existe o no pertenece al catálogo de la sucursal.")
            : new GetCatalogProductDetailResult(
                CatalogQueryStatus.Success,
                product);
    }
}
