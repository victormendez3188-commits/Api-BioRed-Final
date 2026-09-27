using BioRed.Application.Organizations;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Organizations;

public sealed class BranchManagementService : IBranchManagementService
{
    private const decimal MaximumMoney = 9_999_999_999_999_999.99m;
    private readonly BioRedDbContext _dbContext;

    public BranchManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedBranchesResponse> GetAsync(
        GetManagedBranchesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Sucursales.AsNoTracking().AsQueryable();

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
                item.CodSucursal.Contains(search) ||
                item.NombreSucursal.Contains(search) ||
                item.EmailSucursal.Contains(search));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await
        (
            from branch in query
            join company in _dbContext.Empresas.AsNoTracking()
                on branch.CodEmpresa equals company.CodEmpresa
            join configuration in _dbContext.ConfiguracionesEntrega.AsNoTracking()
                on branch.CodSucursal equals configuration.CodSucursal
                into deliveryConfigurations
            from configuration in deliveryConfigurations.DefaultIfEmpty()
            orderby branch.NombreSucursal, branch.CodSucursal
            select new
            {
                Branch = branch,
                company.NombreEmpresa,
                Configuration = configuration
            }
        )
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(item => Map(
                item.Branch,
                item.NombreEmpresa,
                item.Configuration))
            .ToArray();

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

    public async Task<BranchManagementResult> GetByCodeAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(branchCode);
        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    public async Task<BranchManagementResult> CreateAsync(
        CreateManagedBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HasValidCoordinates(request.Latitude, request.Longitude))
        {
            return InvalidCoordinates();
        }

        var branchCode = NormalizeCode(request.BranchCode);
        var companyCode = NormalizeCode(request.CompanyCode);
        var email = NormalizeEmail(request.Email);

        var companyExists = await _dbContext.Empresas
            .AsNoTracking()
            .AnyAsync(
                item => item.CodEmpresa == companyCode && item.Estado,
                cancellationToken);

        if (!companyExists)
        {
            return Failure(
                BranchManagementStatus.CompanyNotFound,
                $"No existe la empresa activa {companyCode}.");
        }

        if (await _dbContext.Sucursales.AsNoTracking().AnyAsync(
                item => item.CodSucursal == branchCode,
                cancellationToken))
        {
            return Failure(
                BranchManagementStatus.DuplicateCode,
                $"Ya existe la sucursal {branchCode}.");
        }

        if (await _dbContext.Sucursales.AsNoTracking().AnyAsync(
                item => item.EmailSucursal == email,
                cancellationToken))
        {
            return Failure(
                BranchManagementStatus.DuplicateEmail,
                "Ya existe una sucursal con ese correo electrónico.");
        }

        var branch = new Sucursal
        {
            CodSucursal = branchCode,
            CodEmpresa = companyCode,
            NombreSucursal = request.BranchName.Trim(),
            DireccionSucursal = request.Address.Trim(),
            TelefonoSucursal = request.Phone.Trim(),
            EmailSucursal = email,
            Latitud = request.Latitude,
            Longitud = request.Longitude,
            Estado = true,
            CreadoEl = DateTime.UtcNow
        };

        try
        {
            _dbContext.Sucursales.Add(branch);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                BranchManagementStatus.DuplicateData,
                "El código o correo de la sucursal ya está registrado.");
        }

        var companyName = await FindCompanyNameAsync(
            companyCode,
            cancellationToken);

        return new(
            BranchManagementStatus.Success,
            Map(branch, companyName ?? companyCode, null));
    }

    public async Task<BranchManagementResult> UpdateAsync(
        string branchCode,
        UpdateManagedBranchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HasValidCoordinates(request.Latitude, request.Longitude))
        {
            return InvalidCoordinates();
        }

        var normalizedCode = NormalizeCode(branchCode);
        var email = NormalizeEmail(request.Email);
        var branch = await _dbContext.Sucursales.SingleOrDefaultAsync(
            item => item.CodSucursal == normalizedCode,
            cancellationToken);

        if (branch is null)
        {
            return NotFound(normalizedCode);
        }

        if (request.Active == true)
        {
            var companyIsActive = await _dbContext.Empresas
                .AsNoTracking()
                .AnyAsync(
                    item => item.CodEmpresa == branch.CodEmpresa && item.Estado,
                    cancellationToken);

            if (!companyIsActive)
            {
                return Failure(
                    BranchManagementStatus.CompanyNotFound,
                    "No puede activar una sucursal cuya empresa está inactiva.");
            }
        }

        var duplicateEmail = await _dbContext.Sucursales
            .AsNoTracking()
            .AnyAsync(
                item => item.CodSucursal != normalizedCode &&
                        item.EmailSucursal == email,
                cancellationToken);

        if (duplicateEmail)
        {
            return Failure(
                BranchManagementStatus.DuplicateEmail,
                "Ya existe una sucursal con ese correo electrónico.");
        }

        branch.NombreSucursal = request.BranchName.Trim();
        branch.DireccionSucursal = request.Address.Trim();
        branch.TelefonoSucursal = request.Phone.Trim();
        branch.EmailSucursal = email;
        branch.Latitud = request.Latitude;
        branch.Longitud = request.Longitude;

        if (request.Active.HasValue)
        {
            branch.Estado = request.Active.Value;
        }

        if (!branch.Estado ||
            !branch.Latitud.HasValue ||
            !branch.Longitud.HasValue)
        {
            var configuration = await _dbContext.ConfiguracionesEntrega
                .SingleOrDefaultAsync(
                    item => item.CodSucursal == normalizedCode,
                    cancellationToken);

            if (configuration is not null)
            {
                configuration.Activo = false;
            }
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
                BranchManagementStatus.DuplicateEmail,
                "Ya existe una sucursal con ese correo electrónico.");
        }

        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    public async Task<BranchManagementResult> DeactivateAsync(
        string branchCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(branchCode);
        var branch = await _dbContext.Sucursales.SingleOrDefaultAsync(
            item => item.CodSucursal == normalizedCode && item.Estado,
            cancellationToken);

        if (branch is null)
        {
            return NotFound(normalizedCode);
        }

        branch.Estado = false;
        var configuration = await _dbContext.ConfiguracionesEntrega
            .SingleOrDefaultAsync(
                item => item.CodSucursal == normalizedCode,
                cancellationToken);

        if (configuration is not null)
        {
            configuration.Activo = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    public async Task<BranchManagementResult> UpsertDeliveryConfigurationAsync(
        string branchCode,
        UpsertDeliveryConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HasValidDeliveryConfiguration(request))
        {
            return Failure(
                BranchManagementStatus.InvalidDeliveryConfiguration,
                "Verifique el radio, las tarifas y el tiempo estimado.");
        }

        var normalizedCode = NormalizeCode(branchCode);
        var branch = await _dbContext.Sucursales.SingleOrDefaultAsync(
            item => item.CodSucursal == normalizedCode,
            cancellationToken);

        if (branch is null)
        {
            return NotFound(normalizedCode);
        }

        if (request.Active &&
            (!branch.Estado ||
             !branch.Latitud.HasValue ||
             !branch.Longitud.HasValue))
        {
            return Failure(
                BranchManagementStatus.InvalidCoordinates,
                "La sucursal debe estar activa y tener coordenadas para habilitar entregas.");
        }

        var configuration = await _dbContext.ConfiguracionesEntrega
            .SingleOrDefaultAsync(
                item => item.CodSucursal == normalizedCode,
                cancellationToken);

        if (configuration is null)
        {
            configuration = new ConfiguracionEntregaSucursal
            {
                CodSucursal = normalizedCode
            };

            _dbContext.ConfiguracionesEntrega.Add(configuration);
        }

        configuration.RadioMaximoKm = request.MaximumRadiusKm;
        configuration.TarifaBase = request.BaseRate;
        configuration.TarifaPorKmExtra = request.ExtraKilometerRate;
        configuration.TiempoEstimadoMin = request.EstimatedMinutes;
        configuration.Activo = request.Active;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                BranchManagementStatus.DuplicateData,
                "La sucursal ya tiene una configuración de entrega.");
        }

        return await GetResultAsync(normalizedCode, cancellationToken);
    }

    private async Task<BranchManagementResult> GetResultAsync(
        string branchCode,
        CancellationToken cancellationToken)
    {
        var branch = await _dbContext.Sucursales
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodSucursal == branchCode,
                cancellationToken);

        if (branch is null)
        {
            return NotFound(branchCode);
        }

        var companyName = await FindCompanyNameAsync(
            branch.CodEmpresa,
            cancellationToken);

        var configuration = await _dbContext.ConfiguracionesEntrega
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodSucursal == branchCode,
                cancellationToken);

        return new(
            BranchManagementStatus.Success,
            Map(branch, companyName ?? branch.CodEmpresa, configuration));
    }

    private async Task<string?> FindCompanyNameAsync(
        string companyCode,
        CancellationToken cancellationToken) =>
        await _dbContext.Empresas
            .AsNoTracking()
            .Where(item => item.CodEmpresa == companyCode)
            .Select(item => item.NombreEmpresa)
            .SingleOrDefaultAsync(cancellationToken);

    private static bool HasValidCoordinates(
        decimal? latitude,
        decimal? longitude) =>
        latitude.HasValue == longitude.HasValue &&
        (!latitude.HasValue ||
         (latitude.Value is >= -90m and <= 90m &&
          longitude!.Value is >= -180m and <= 180m));

    private static bool HasValidDeliveryConfiguration(
        UpsertDeliveryConfigurationRequest request) =>
        request.MaximumRadiusKm is > 0m and <= 100m &&
        request.BaseRate is >= 0m and <= MaximumMoney &&
        request.ExtraKilometerRate is >= 0m and <= MaximumMoney &&
        request.EstimatedMinutes is >= 1 and <= 1_440;

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    private static ManagedBranchResponse Map(
        Sucursal branch,
        string companyName,
        ConfiguracionEntregaSucursal? configuration) =>
        new(
            branch.CodSucursal,
            branch.CodEmpresa,
            companyName,
            branch.NombreSucursal,
            branch.DireccionSucursal,
            branch.TelefonoSucursal,
            branch.EmailSucursal,
            branch.Latitud,
            branch.Longitud,
            branch.Estado,
            branch.CreadoEl,
            configuration is null
                ? null
                : new DeliveryConfigurationResponse(
                    configuration.RadioMaximoKm,
                    configuration.TarifaBase,
                    configuration.TarifaPorKmExtra,
                    configuration.TiempoEstimadoMin,
                    configuration.Activo));

    private static BranchManagementResult NotFound(string branchCode) =>
        Failure(
            BranchManagementStatus.NotFound,
            $"No existe la sucursal {branchCode}.");

    private static BranchManagementResult InvalidCoordinates() =>
        Failure(
            BranchManagementStatus.InvalidCoordinates,
            "Debe enviar latitud y longitud juntas y dentro de los rangos válidos.");

    private static BranchManagementResult Failure(
        BranchManagementStatus status,
        string detail) =>
        new(status, Detail: detail);
}
