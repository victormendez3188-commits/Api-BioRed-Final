using System.Data;
using BioRed.Application.Drivers;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using BioRed.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Drivers;

internal sealed class DriverProfileService : IDriverProfileService
{
    private readonly BioRedDbContext _dbContext;
    private readonly TemporaryPasswordProvisioner _temporaryPasswordProvisioner;

    public DriverProfileService(
        BioRedDbContext dbContext,
        TemporaryPasswordProvisioner temporaryPasswordProvisioner)
    {
        _dbContext = dbContext;
        _temporaryPasswordProvisioner = temporaryPasswordProvisioner;
    }

    public async Task<DriverProfileResult> RegisterAsync(
        RegisterDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var emailConfigurationError =
            _temporaryPasswordProvisioner.ValidateEmailConfiguration();

        if (emailConfigurationError is not null)
        {
            return Failure(
                DriverProfileStatus.EmailConfigurationInvalid,
                emailConfigurationError);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var document = request.Document.Trim().ToUpperInvariant();
        var licensePlate = NormalizeLicensePlate(request.LicensePlate);

        var duplicateResult = await FindDuplicateAsync(
            email,
            document,
            licensePlate,
            null,
            cancellationToken);

        if (duplicateResult is not null)
        {
            return duplicateResult;
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                var duplicateInsideTransaction = await FindDuplicateAsync(
                    email,
                    document,
                    licensePlate,
                    null,
                    cancellationToken);

                if (duplicateInsideTransaction is not null)
                {
                    return duplicateInsideTransaction;
                }

                var driverCode = await CreateDriverCodeAsync(cancellationToken);
                var createdAtUtc = DateTime.UtcNow;

                var driver = new Repartidor
                {
                    CodRepartidor = driverCode,
                    NombreRepartidor = request.FirstName.Trim(),
                    ApellidosRepartidor = request.LastName.Trim(),
                    Telefono = request.Phone.Trim(),
                    DpiDocumento = document,
                    TipoVehiculo = request.VehicleType.Trim(),
                    PlacaVehiculo = licensePlate,
                    LatitudActual = null,
                    LongitudActual = null,
                    EstadoDisponibilidad = "Desconectado",
                    Estado = true,
                    CreadoEl = createdAtUtc
                };

                var account = new CuentaAcceso
                {
                    Correo = email,
                    TipoUsuario = "Repartidor",
                    ReferenciaId = driverCode,
                    Estado = true,
                    CreadoEl = createdAtUtc
                };

                var preparedPassword =
                    _temporaryPasswordProvisioner.Prepare(account);

                _dbContext.Repartidores.Add(driver);
                _dbContext.CuentasAcceso.Add(account);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var emailSent =
                    await _temporaryPasswordProvisioner.SendAsync(
                        account,
                        $"{driver.NombreRepartidor} {driver.ApellidosRepartidor}",
                        preparedPassword,
                        cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (!emailSent)
                {
                    return Failure(
                        DriverProfileStatus.EmailDeliveryFailed,
                        "El repartidor fue creado, pero no se pudo enviar la contraseña temporal. Administración debe reenviarla.");
                }

                return new DriverProfileResult(
                    DriverProfileStatus.Success,
                    Map(
                        driver,
                        email,
                        account.RequiereCambioPassword,
                        account.PasswordTemporalExpiraUtc,
                        account.EstadoEnvioPasswordTemporal));
            });
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                DriverProfileStatus.DuplicateData,
                "El correo, documento o placa ya está registrado.");
        }
    }

    public async Task<DriverProfileResult> GetAsync(
        string driverCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = driverCode.Trim().ToUpperInvariant();

        var profile = await
        (
            from driver in _dbContext.Repartidores.AsNoTracking()
            join account in _dbContext.CuentasAcceso.AsNoTracking()
                on driver.CodRepartidor equals account.ReferenciaId
            where driver.CodRepartidor == normalizedCode &&
                  driver.Estado &&
                  account.TipoUsuario == "Repartidor" &&
                  account.Estado
            select new DriverProfileResponse(
                driver.CodRepartidor,
                driver.NombreRepartidor,
                driver.ApellidosRepartidor,
                driver.DpiDocumento,
                driver.Telefono,
                account.Correo,
                driver.TipoVehiculo,
                driver.PlacaVehiculo,
                driver.EstadoDisponibilidad,
                driver.LatitudActual,
                driver.LongitudActual,
                driver.Estado,
                driver.CreadoEl,
                false,
                null,
                null)
        )
        .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? NotFound()
            : new(DriverProfileStatus.Success, profile);
    }

    public async Task<DriverProfileResult> UpdateAsync(
        string driverCode,
        UpdateDriverProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = driverCode.Trim().ToUpperInvariant();
        var driver = await _dbContext.Repartidores
            .SingleOrDefaultAsync(
                item => item.CodRepartidor == normalizedCode && item.Estado,
                cancellationToken);

        if (driver is null)
        {
            return NotFound();
        }

        var email = await FindEmailAsync(normalizedCode, cancellationToken);
        if (email is null)
        {
            return NotFound();
        }

        var licensePlate = NormalizeLicensePlate(request.LicensePlate);
        if (licensePlate is not null)
        {
            var duplicatePlate = await _dbContext.Repartidores
                .AsNoTracking()
                .AnyAsync(
                    item => item.CodRepartidor != normalizedCode &&
                            item.PlacaVehiculo == licensePlate,
                    cancellationToken);

            if (duplicatePlate)
            {
                return Failure(
                    DriverProfileStatus.DuplicateLicensePlate,
                    "Ya existe un repartidor con esa placa.");
            }
        }

        driver.NombreRepartidor = request.FirstName.Trim();
        driver.ApellidosRepartidor = request.LastName.Trim();
        driver.Telefono = request.Phone.Trim();
        driver.TipoVehiculo = request.VehicleType.Trim();
        driver.PlacaVehiculo = licensePlate;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                DriverProfileStatus.DuplicateLicensePlate,
                "Ya existe un repartidor con esa placa.");
        }

        return new(
            DriverProfileStatus.Success,
            Map(driver, email));
    }

    public async Task<DriverProfileResult> UpdateAvailabilityAsync(
        string driverCode,
        UpdateDriverAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = driverCode.Trim().ToUpperInvariant();
        var driver = await _dbContext.Repartidores
            .SingleOrDefaultAsync(
                item => item.CodRepartidor == normalizedCode && item.Estado,
                cancellationToken);

        if (driver is null)
        {
            return NotFound();
        }

        if (string.Equals(
                driver.EstadoDisponibilidad,
                "Ocupado",
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                DriverProfileStatus.Busy,
                "No puede cambiar la disponibilidad mientras tiene un pedido activo.");
        }

        var email = await FindEmailAsync(normalizedCode, cancellationToken);
        if (email is null)
        {
            return NotFound();
        }

        driver.EstadoDisponibilidad = NormalizeAvailability(request.Availability);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            DriverProfileStatus.Success,
            Map(driver, email));
    }

    private async Task<DriverProfileResult?> FindDuplicateAsync(
        string email,
        string document,
        string? licensePlate,
        string? excludedDriverCode,
        CancellationToken cancellationToken)
    {
        var emailExists = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .AnyAsync(item => item.Correo == email, cancellationToken);

        if (emailExists)
        {
            return Failure(
                DriverProfileStatus.DuplicateEmail,
                "Ya existe una cuenta con ese correo electrónico.");
        }

        var documentExists = await _dbContext.Repartidores
            .AsNoTracking()
            .AnyAsync(
                item => item.DpiDocumento == document &&
                        item.CodRepartidor != excludedDriverCode,
                cancellationToken);

        if (documentExists)
        {
            return Failure(
                DriverProfileStatus.DuplicateDocument,
                "Ya existe un repartidor con ese documento.");
        }

        if (licensePlate is null)
        {
            return null;
        }

        var licensePlateExists = await _dbContext.Repartidores
            .AsNoTracking()
            .AnyAsync(
                item => item.PlacaVehiculo == licensePlate &&
                        item.CodRepartidor != excludedDriverCode,
                cancellationToken);

        return licensePlateExists
            ? Failure(
                DriverProfileStatus.DuplicateLicensePlate,
                "Ya existe un repartidor con esa placa.")
            : null;
    }

    private async Task<string?> FindEmailAsync(
        string driverCode,
        CancellationToken cancellationToken) =>
        await _dbContext.CuentasAcceso
            .AsNoTracking()
            .Where(item =>
                item.TipoUsuario == "Repartidor" &&
                item.ReferenciaId == driverCode &&
                item.Estado)
            .Select(item => item.Correo)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string> CreateDriverCodeAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"REP-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
            var exists = await _dbContext.Repartidores
                .AsNoTracking()
                .AnyAsync(
                    item => item.CodRepartidor == code,
                    cancellationToken);

            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            "No fue posible generar un código único para el repartidor.");
    }

    private static string NormalizeAvailability(string availability) =>
        availability.Trim().Equals(
            "Disponible",
            StringComparison.OrdinalIgnoreCase)
            ? "Disponible"
            : "Desconectado";

    private static string? NormalizeLicensePlate(string? licensePlate) =>
        string.IsNullOrWhiteSpace(licensePlate)
            ? null
            : licensePlate.Trim().ToUpperInvariant();

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private static DriverProfileResponse Map(
        Repartidor driver,
        string email,
        bool passwordChangeRequired = false,
        DateTime? temporaryPasswordExpiresAtUtc = null,
        string? temporaryPasswordEmailStatus = null) =>
        new(
            driver.CodRepartidor,
            driver.NombreRepartidor,
            driver.ApellidosRepartidor,
            driver.DpiDocumento,
            driver.Telefono,
            email,
            driver.TipoVehiculo,
            driver.PlacaVehiculo,
            string.IsNullOrWhiteSpace(driver.EstadoDisponibilidad)
                ? "Desconectado"
                : driver.EstadoDisponibilidad,
            driver.LatitudActual,
            driver.LongitudActual,
            driver.Estado,
            driver.CreadoEl,
            passwordChangeRequired,
            temporaryPasswordExpiresAtUtc,
            temporaryPasswordEmailStatus);

    private static DriverProfileResult NotFound() =>
        Failure(
            DriverProfileStatus.NotFound,
            "No existe un perfil activo para el repartidor autenticado.");

    private static DriverProfileResult Failure(
        DriverProfileStatus status,
        string detail) =>
        new(status, Detail: detail);
}
