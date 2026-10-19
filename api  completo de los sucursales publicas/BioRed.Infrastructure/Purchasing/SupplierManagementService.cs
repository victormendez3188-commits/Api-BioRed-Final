using BioRed.Application.Purchasing;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Purchasing;

public sealed class SupplierManagementService : ISupplierManagementService
{
    private readonly BioRedDbContext _dbContext;

    public SupplierManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedSuppliersResponse> GetAsync(
        GetManagedSuppliersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Proveedores.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(item => item.Estado);
        }

        if (!string.IsNullOrWhiteSpace(request.CompanyCode))
        {
            var companyCode = NormalizeCode(request.CompanyCode);
            query = query.Where(item => item.CodEmpresa == companyCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.CodProveedor.Contains(search) ||
                item.NombreProveedor.Contains(search) ||
                item.CuiNit.Contains(search) ||
                item.RazonSocial.Contains(search) ||
                item.EmailProveedor.Contains(search));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await
        (
            from supplier in query
            join company in _dbContext.Empresas.AsNoTracking()
                on supplier.CodEmpresa equals company.CodEmpresa
            orderby supplier.NombreProveedor, supplier.CodProveedor
            select new { Supplier = supplier, company.NombreEmpresa }
        )
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(item => Map(item.Supplier, item.NombreEmpresa))
            .ToArray();

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(request.Page, request.PageSize, totalItems, totalPages, items);
    }

    public async Task<SupplierManagementResult> GetByCodeAsync(
        string supplierCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(supplierCode);
        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    public async Task<SupplierManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplierCode = NormalizeCode(request.SupplierCode);
        var companyCode = NormalizeCode(request.CompanyCode);
        var taxId = NormalizeIdentifier(request.TaxId);
        var email = NormalizeEmail(request.Email);

        var companyExists = await _dbContext.Empresas
            .AsNoTracking()
            .AnyAsync(
                item => item.CodEmpresa == companyCode && item.Estado,
                cancellationToken);

        if (!companyExists)
        {
            return Failure(
                SupplierManagementStatus.CompanyNotFound,
                $"No existe la empresa activa {companyCode}.");
        }

        if (await _dbContext.Proveedores.AsNoTracking().AnyAsync(
                item => item.CodProveedor == supplierCode,
                cancellationToken))
        {
            return Failure(
                SupplierManagementStatus.DuplicateCode,
                $"Ya existe el proveedor {supplierCode}.");
        }

        if (await _dbContext.Proveedores.AsNoTracking().AnyAsync(
                item => item.CuiNit == taxId,
                cancellationToken))
        {
            return Failure(
                SupplierManagementStatus.DuplicateTaxId,
                "Ya existe un proveedor con ese CUI o NIT.");
        }

        if (await _dbContext.Proveedores.AsNoTracking().AnyAsync(
                item => item.EmailProveedor == email,
                cancellationToken))
        {
            return Failure(
                SupplierManagementStatus.DuplicateEmail,
                "Ya existe un proveedor con ese correo electrónico.");
        }

        var supplier = new Proveedor
        {
            CodProveedor = supplierCode,
            CodEmpresa = companyCode,
            NombreProveedor = request.SupplierName.Trim(),
            CuiNit = taxId,
            RazonSocial = request.BusinessName.Trim(),
            TelefonoProveedor = request.Phone.Trim(),
            EmailProveedor = email,
            Estado = true,
            CreadoEl = DateTime.UtcNow,
            CreadoPor = NormalizeCode(actorReferenceId)
        };

        try
        {
            _dbContext.Proveedores.Add(supplier);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                SupplierManagementStatus.DuplicateData,
                "El código, CUI/NIT o correo del proveedor ya está registrado.");
        }

        return await GetResultAsync(supplierCode, cancellationToken);
    }

    public async Task<SupplierManagementResult> UpdateAsync(
        string supplierCode,
        UpdateManagedSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(supplierCode);
        var taxId = NormalizeIdentifier(request.TaxId);
        var email = NormalizeEmail(request.Email);
        var supplier = await _dbContext.Proveedores.SingleOrDefaultAsync(
            item => item.CodProveedor == normalizedCode,
            cancellationToken);

        if (supplier is null)
        {
            return NotFound(normalizedCode);
        }

        if (await _dbContext.Proveedores.AsNoTracking().AnyAsync(
                item => item.CodProveedor != normalizedCode && item.CuiNit == taxId,
                cancellationToken))
        {
            return Failure(
                SupplierManagementStatus.DuplicateTaxId,
                "Ya existe un proveedor con ese CUI o NIT.");
        }

        if (await _dbContext.Proveedores.AsNoTracking().AnyAsync(
                item => item.CodProveedor != normalizedCode &&
                        item.EmailProveedor == email,
                cancellationToken))
        {
            return Failure(
                SupplierManagementStatus.DuplicateEmail,
                "Ya existe un proveedor con ese correo electrónico.");
        }

        supplier.NombreProveedor = request.SupplierName.Trim();
        supplier.CuiNit = taxId;
        supplier.RazonSocial = request.BusinessName.Trim();
        supplier.TelefonoProveedor = request.Phone.Trim();
        supplier.EmailProveedor = email;

        if (request.Active.HasValue)
        {
            supplier.Estado = request.Active.Value;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                SupplierManagementStatus.DuplicateData,
                "El CUI/NIT o correo del proveedor ya está registrado.");
        }

        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    public async Task<SupplierManagementResult> DeactivateAsync(
        string supplierCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(supplierCode);
        var supplier = await _dbContext.Proveedores.SingleOrDefaultAsync(
            item => item.CodProveedor == normalizedCode && item.Estado,
            cancellationToken);

        if (supplier is null)
        {
            return NotFound(normalizedCode);
        }

        supplier.Estado = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    private async Task<SupplierManagementResult> GetResultAsync(
        string supplierCode,
        CancellationToken cancellationToken)
    {
        var row = await
        (
            from supplier in _dbContext.Proveedores.AsNoTracking()
            join company in _dbContext.Empresas.AsNoTracking()
                on supplier.CodEmpresa equals company.CodEmpresa
            where supplier.CodProveedor == supplierCode
            select new { Supplier = supplier, company.NombreEmpresa }
        ).SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? NotFound(supplierCode)
            : new(
                SupplierManagementStatus.Success,
                Map(row.Supplier, row.NombreEmpresa));
    }

    private static ManagedSupplierResponse Map(
        Proveedor supplier,
        string companyName) =>
        new(
            supplier.CodProveedor,
            supplier.CodEmpresa,
            companyName,
            supplier.NombreProveedor,
            supplier.CuiNit,
            supplier.RazonSocial,
            supplier.TelefonoProveedor,
            supplier.EmailProveedor,
            supplier.Estado,
            supplier.CreadoEl,
            supplier.CreadoPor);

    private static SupplierManagementResult NotFound(string supplierCode) =>
        Failure(
            SupplierManagementStatus.NotFound,
            $"No existe el proveedor {supplierCode}.");

    private static SupplierManagementResult Failure(
        SupplierManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeIdentifier(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;
}
