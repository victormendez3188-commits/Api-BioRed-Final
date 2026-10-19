using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Security;

public sealed class AdminSeeder
{
    private readonly BioRedDbContext _dbContext;
    private readonly IPasswordHasher<CuentaAcceso> _passwordHasher;
    private readonly AdminSeedOptions _options;
    private readonly ILogger<AdminSeeder> _logger;

    public AdminSeeder(
        BioRedDbContext dbContext,
        IPasswordHasher<CuentaAcceso> passwordHasher,
        IOptions<AdminSeedOptions> options,
        ILogger<AdminSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        ValidateOptions();

        var referenceId = _options.ReferenceId.Trim();
        var email = _options.Email.Trim();

        var employeeExists = await _dbContext.Usuarios
            .AsNoTracking()
            .AnyAsync(
                item => item.CodUsuario == referenceId && item.Estado,
                cancellationToken);

        if (!employeeExists)
        {
            throw new InvalidOperationException(
                $"No existe el empleado activo '{referenceId}' requerido por AdminSeed.");
        }

        var accountExists = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .AnyAsync(
                item => item.Correo == email ||
                        (item.TipoUsuario == "Empleado" && item.ReferenciaId == referenceId),
                cancellationToken);

        if (accountExists)
        {
            _logger.LogInformation("La cuenta administrativa inicial ya existe.");
            return;
        }

        var account = new CuentaAcceso
        {
            Correo = email,
            TipoUsuario = "Empleado",
            ReferenciaId = referenceId,
            Estado = true,
            CreadoEl = DateTime.UtcNow
        };

        account.PasswordHash = _passwordHasher.HashPassword(account, _options.Password);
        _dbContext.CuentasAcceso.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cuenta administrativa inicial creada. IdCuenta: {IdCuenta}",
            account.IdCuenta);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ReferenceId) ||
            _options.ReferenceId.Trim().Length > 15)
        {
            throw new InvalidOperationException(
                "AdminSeed:ReferenceId es obligatorio y admite hasta 15 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(_options.Email) ||
            _options.Email.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "AdminSeed:Email es obligatorio y admite hasta 100 caracteres.");
        }

        var password = _options.Password;
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException(
                "AdminSeed:Password debe tener al menos 12 caracteres, mayúscula, " +
                "minúscula, número y carácter especial.");
        }
    }
}
