using BioRed.Application.Catalog;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using BioRed.Infrastructure.Products;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Catalog;

public sealed class CatalogService : ICatalogService
{
    private readonly BioRedDbContext _dbContext;
    private readonly UnitSaleOptions _unitOptions;

    public CatalogService(
        BioRedDbContext dbContext,
        IOptions<UnitSaleOptions> unitOptions)
    {
        _dbContext = dbContext;
        _unitOptions = unitOptions.Value;
    }

    public async Task<IReadOnlyCollection<CatalogPaymentTypeResponse>> GetPaymentTypesAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.TiposPago
            .AsNoTracking()
            .Where(item => item.Estado)
            .OrderBy(item => item.NombreTipoPago)
            .ThenBy(item => item.CodTipoPago)
            .Select(item => new CatalogPaymentTypeResponse(
                item.CodTipoPago,
                item.NombreTipoPago))
            .ToArrayAsync(cancellationToken);

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
            .ToListAsync(cancellationToken);

        var unitSaleByCode = await GetUnitSaleByCodeAsync(
            products.Select(item => item.CodProducto),
            cancellationToken);

        var productResponses = products
            .Select(item =>
            {
                unitSaleByCode.TryGetValue(item.CodProducto, out var unitSale);
                var availablePresentations = unitSale is null
                    ? (int?)null
                    : item.CantidadActual / unitSale.UnidadesPorPresentacion;
                return new CatalogProductResponse(
                    item.CodProducto,
                    item.NombreProducto,
                    item.DescripcionProducto,
                    item.PrecioVenta,
                    item.CantidadActual,
                    item.CantidadActual > 0,
                    item.RequiereReceta,
                    unitSale is not null,
                    unitSale?.PrecioPorUnidad,
                    unitSale?.UnidadBase,
                    unitSale?.UnidadesPorPresentacion,
                    unitSale is null ? null : item.CantidadActual,
                    availablePresentations);
            })
            .ToArray();

        return new GetCatalogProductsResult(
            CatalogQueryStatus.Success,
            new GetCatalogProductsResponse(
                branch.CodSucursal,
                branch.NombreSucursal,
                request.Page,
                request.PageSize,
                totalItems,
                totalPages,
                productResponses));
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
            select new
            {
                branch.CodSucursal,
                branch.NombreSucursal,
                item.CodProducto,
                item.NombreProducto,
                item.DescripcionProducto,
                item.PrecioVenta,
                inventory.CantidadActual,
                inventory.StockMinimo,
                inventory.StockMaximo,
                inventory.UltimaActualizacion,
                item.RequiereReceta
            }
        )
        .SingleOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return new GetCatalogProductDetailResult(
                CatalogQueryStatus.ProductNotFound,
                Detail: "El producto no existe o no pertenece al catálogo de la sucursal.");
        }

        var unitSaleByCode = await GetUnitSaleByCodeAsync(
            new[] { product.CodProducto },
            cancellationToken);
        unitSaleByCode.TryGetValue(product.CodProducto, out var unitSale);
        var availablePresentations = unitSale is null
            ? (int?)null
            : product.CantidadActual / unitSale.UnidadesPorPresentacion;

        return new GetCatalogProductDetailResult(
            CatalogQueryStatus.Success,
            new CatalogProductDetailResponse(
                product.CodSucursal,
                product.NombreSucursal,
                product.CodProducto,
                product.NombreProducto,
                product.DescripcionProducto,
                product.PrecioVenta,
                product.CantidadActual,
                product.StockMinimo,
                product.StockMaximo,
                product.CantidadActual > 0,
                product.UltimaActualizacion,
                product.RequiereReceta,
                unitSale is not null,
                unitSale?.PrecioPorUnidad,
                unitSale?.UnidadBase,
                unitSale?.UnidadesPorPresentacion,
                unitSale is null ? null : product.CantidadActual,
                availablePresentations));
    }

    private async Task<Dictionary<string, ProductoVentaUnidad>> GetUnitSaleByCodeAsync(
        IEnumerable<string> productCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = productCodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!_unitOptions.Enabled || normalizedCodes.Length == 0)
        {
            return new Dictionary<string, ProductoVentaUnidad>(StringComparer.OrdinalIgnoreCase);
        }

        return (await _dbContext.ProductosVentaUnidad
                .AsNoTracking()
                .Where(item =>
                    normalizedCodes.Contains(item.CodProducto) &&
                    item.Activo &&
                    item.PermiteVentaUnidad)
                .ToListAsync(cancellationToken))
            .ToDictionary(item => item.CodProducto, StringComparer.OrdinalIgnoreCase);
    }

}
