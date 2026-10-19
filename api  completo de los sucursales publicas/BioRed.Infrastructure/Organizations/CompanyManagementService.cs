using BioRed.Application.Organizations;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Organizations;

public sealed class CompanyManagementService : ICompanyManagementService
{
    private readonly BioRedDbContext _dbContext;

    public CompanyManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedCompaniesResponse> GetAsync(
        GetManagedCompaniesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Empresas.AsNoTracking().AsQueryable();

        if (!request.IncludeInactive)
        {
            query = query.Where(item => item.Estado);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.CodEmpresa.Contains(search) ||
                item.NombreEmpresa.Contains(search) ||
                item.EmailEmpresa.Contains(search));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.NombreEmpresa)
            .ThenBy(item => item.CodEmpresa)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new ManagedCompanyResponse(
                item.CodEmpresa,
                item.NombreEmpresa,
                item.DireccionEmpresa,
                item.TelefonoEmpresa,
                item.EmailEmpresa,
                item.Estado,
                item.CreadoEl))
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

    public async Task<CompanyManagementResult> GetByCodeAsync(
        string companyCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(companyCode);
        var company = await _dbContext.Empresas
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodEmpresa == normalizedCode,
                cancellationToken);

        return company is null
            ? NotFound(normalizedCode)
            : new(CompanyManagementStatus.Success, Map(company));
    }

    public async Task<CompanyManagementResult> CreateAsync(
        CreateManagedCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyCode = NormalizeCode(request.CompanyCode);
        var email = NormalizeEmail(request.Email);

        if (await _dbContext.Empresas.AsNoTracking().AnyAsync(
                item => item.CodEmpresa == companyCode,
                cancellationToken))
        {
            return Failure(
                CompanyManagementStatus.DuplicateCode,
                $"Ya existe la empresa {companyCode}.");
        }

        if (await _dbContext.Empresas.AsNoTracking().AnyAsync(
                item => item.EmailEmpresa == email,
                cancellationToken))
        {
            return Failure(
                CompanyManagementStatus.DuplicateEmail,
                "Ya existe una empresa con ese correo electrónico.");
        }

        var company = new Empresa
        {
            CodEmpresa = companyCode,
            NombreEmpresa = request.CompanyName.Trim(),
            DireccionEmpresa = request.Address.Trim(),
            TelefonoEmpresa = request.Phone.Trim(),
            EmailEmpresa = email,
            Estado = true,
            CreadoEl = DateTime.UtcNow
        };

        try
        {
            _dbContext.Empresas.Add(company);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                CompanyManagementStatus.DuplicateData,
                "El código o correo de la empresa ya está registrado.");
        }

        return new(CompanyManagementStatus.Success, Map(company));
    }

    public async Task<CompanyManagementResult> UpdateAsync(
        string companyCode,
        UpdateManagedCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = NormalizeCode(companyCode);
        var email = NormalizeEmail(request.Email);
        var company = await _dbContext.Empresas.SingleOrDefaultAsync(
            item => item.CodEmpresa == normalizedCode,
            cancellationToken);

        if (company is null)
        {
            return NotFound(normalizedCode);
        }

        var duplicateEmail = await _dbContext.Empresas
            .AsNoTracking()
            .AnyAsync(
                item => item.CodEmpresa != normalizedCode &&
                        item.EmailEmpresa == email,
                cancellationToken);

        if (duplicateEmail)
        {
            return Failure(
                CompanyManagementStatus.DuplicateEmail,
                "Ya existe una empresa con ese correo electrónico.");
        }

        company.NombreEmpresa = request.CompanyName.Trim();
        company.DireccionEmpresa = request.Address.Trim();
        company.TelefonoEmpresa = request.Phone.Trim();
        company.EmailEmpresa = email;

        if (request.Active.HasValue)
        {
            company.Estado = request.Active.Value;
        }

        if (request.Active == false)
        {
            await DeactivateCompanyBranchesAsync(
                normalizedCode,
                cancellationToken);
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
                CompanyManagementStatus.DuplicateEmail,
                "Ya existe una empresa con ese correo electrónico.");
        }

        return new(CompanyManagementStatus.Success, Map(company));
    }

    public async Task<CompanyManagementResult> DeactivateAsync(
        string companyCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(companyCode);
        var company = await _dbContext.Empresas.SingleOrDefaultAsync(
            item => item.CodEmpresa == normalizedCode && item.Estado,
            cancellationToken);

        if (company is null)
        {
            return NotFound(normalizedCode);
        }

        company.Estado = false;
        await DeactivateCompanyBranchesAsync(normalizedCode, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(CompanyManagementStatus.Success, Map(company));
    }

    private async Task DeactivateCompanyBranchesAsync(
        string companyCode,
        CancellationToken cancellationToken)
    {
        var branches = await _dbContext.Sucursales
            .Where(item => item.CodEmpresa == companyCode)
            .ToListAsync(cancellationToken);

        foreach (var branch in branches)
        {
            if (branch.Estado)
            {
                branch.Estado = false;
            }
        }

        var branchCodes = branches.Select(item => item.CodSucursal).ToArray();
        if (branchCodes.Length == 0)
        {
            return;
        }

        var configurations = await _dbContext.ConfiguracionesEntrega
            .Where(item => branchCodes.Contains(item.CodSucursal) && item.Activo)
            .ToListAsync(cancellationToken);

        foreach (var configuration in configurations)
        {
            configuration.Activo = false;
        }
    }

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private static ManagedCompanyResponse Map(Empresa company) =>
        new(
            company.CodEmpresa,
            company.NombreEmpresa,
            company.DireccionEmpresa,
            company.TelefonoEmpresa,
            company.EmailEmpresa,
            company.Estado,
            company.CreadoEl);

    private static CompanyManagementResult NotFound(string companyCode) =>
        Failure(
            CompanyManagementStatus.NotFound,
            $"No existe la empresa {companyCode}.");

    private static CompanyManagementResult Failure(
        CompanyManagementStatus status,
        string detail) =>
        new(status, Detail: detail);
}
