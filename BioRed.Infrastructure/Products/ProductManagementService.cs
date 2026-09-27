using BioRed.Application.Products;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Products;

public sealed class ProductManagementService : IProductManagementService
{
    private const decimal MaximumPrice = 9_999_999_999_999_999.99m;

    private readonly BioRedDbContext _dbContext;

    public ProductManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedProductsResponse> GetAsync(
        GetManagedProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Productos.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(item => item.Estado);
        }

        if (!string.IsNullOrWhiteSpace(request.CompanyCode))
        {
            var companyCode = request.CompanyCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.CodEmpresa == companyCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.CodProducto.Contains(search) ||
                item.NombreProducto.Contains(search));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.NombreProducto)
            .ThenBy(item => item.CodProducto)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new ManagedProductResponse(
                item.CodProducto,
                item.CodEmpresa,
                item.NombreProducto,
                item.DescripcionProducto,
                item.PrecioCosto,
                item.PrecioVenta,
                item.RequiereReceta,
                item.Estado,
                item.CreadoEl,
                item.CreadoPor))
            .ToArrayAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(
            request.Page,
            request.PageSize,
            totalItems,
            totalPages,
            items);
    }

    public async Task<ProductManagementResult> GetByCodeAsync(
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = productCode.Trim().ToUpperInvariant();
        var product = await _dbContext.Productos
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodProducto == normalizedCode,
                cancellationToken);

        return product is null
            ? NotFound(normalizedCode)
            : new(ProductManagementStatus.Success, Map(product));
    }

    public async Task<ProductManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (HasInvalidPrices(request.CostPrice, request.SalePrice))
        {
            return new(
                ProductManagementStatus.InvalidPrice,
                Detail: CreateInvalidPriceDetail(request.CostPrice, request.SalePrice));
        }

        var productCode = request.ProductCode.Trim().ToUpperInvariant();
        var companyCode = request.CompanyCode.Trim().ToUpperInvariant();

        var companyExists = await _dbContext.Empresas
            .AsNoTracking()
            .AnyAsync(
                item => item.CodEmpresa == companyCode && item.Estado,
                cancellationToken);

        if (!companyExists)
        {
            return new(
                ProductManagementStatus.CompanyNotFound,
                Detail: $"No existe una empresa con el código {companyCode}.");
        }

        var duplicate = await _dbContext.Productos.AnyAsync(
            item => item.CodProducto == productCode,
            cancellationToken);

        if (duplicate)
        {
            return new(
                ProductManagementStatus.Duplicate,
                Detail: $"Ya existe el producto {productCode}.");
        }

        var product = new Producto
        {
            CodProducto = productCode,
            CodEmpresa = companyCode,
            NombreProducto = request.ProductName.Trim(),
            DescripcionProducto = CleanOptional(request.Description),
            PrecioCosto = request.CostPrice,
            PrecioVenta = request.SalePrice,
            RequiereReceta = request.RequiresPrescription,
            Estado = true,
            CreadoEl = DateTime.UtcNow,
            CreadoPor = actorReferenceId
        };

        _dbContext.Productos.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(ProductManagementStatus.Success, Map(product));
    }

    public async Task<ProductManagementResult> UpdateAsync(
        string productCode,
        UpdateManagedProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (HasInvalidPrices(request.CostPrice, request.SalePrice))
        {
            return new(
                ProductManagementStatus.InvalidPrice,
                Detail: CreateInvalidPriceDetail(request.CostPrice, request.SalePrice));
        }

        var normalizedCode = productCode.Trim().ToUpperInvariant();
        var product = await _dbContext.Productos.SingleOrDefaultAsync(
            item => item.CodProducto == normalizedCode,
            cancellationToken);

        if (product is null)
        {
            return NotFound(normalizedCode);
        }

        product.NombreProducto = request.ProductName.Trim();
        product.DescripcionProducto = CleanOptional(request.Description);
        product.PrecioCosto = request.CostPrice;
        product.PrecioVenta = request.SalePrice;
        if (request.RequiresPrescription.HasValue)
        {
            product.RequiereReceta = request.RequiresPrescription.Value;
        }
        if (request.Active.HasValue)
        {
            product.Estado = request.Active.Value;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(ProductManagementStatus.Success, Map(product));
    }

    public async Task<ProductManagementResult> DeactivateAsync(
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = productCode.Trim().ToUpperInvariant();
        var product = await _dbContext.Productos.SingleOrDefaultAsync(
            item => item.CodProducto == normalizedCode && item.Estado,
            cancellationToken);

        if (product is null)
        {
            return NotFound(normalizedCode);
        }

        product.Estado = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(ProductManagementStatus.Success, Map(product));
    }

    private static ProductManagementResult NotFound(string productCode) =>
        new(
            ProductManagementStatus.NotFound,
            Detail: $"No existe el producto {productCode}.");

    private static ManagedProductResponse Map(Producto product) =>
        new(
            product.CodProducto,
            product.CodEmpresa,
            product.NombreProducto,
            product.DescripcionProducto,
            product.PrecioCosto,
            product.PrecioVenta,
            product.RequiereReceta,
            product.Estado,
            product.CreadoEl,
            product.CreadoPor);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool HasInvalidPrices(decimal costPrice, decimal salePrice) =>
        costPrice < 0m ||
        salePrice <= 0m ||
        costPrice > MaximumPrice ||
        salePrice > MaximumPrice ||
        salePrice < costPrice;

    private static string CreateInvalidPriceDetail(
        decimal costPrice,
        decimal salePrice)
    {
        if (costPrice > MaximumPrice || salePrice > MaximumPrice)
        {
            return $"Los precios no pueden superar {MaximumPrice:0.00}.";
        }

        if (costPrice < 0m || salePrice <= 0m)
        {
            return "El precio de costo no puede ser negativo y el precio de venta debe ser mayor que cero.";
        }

        return "El precio de venta no puede ser menor que el precio de costo.";
    }
}
