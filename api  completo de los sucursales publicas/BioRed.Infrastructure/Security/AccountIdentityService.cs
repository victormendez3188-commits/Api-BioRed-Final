using BioRed.Application.Security;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Security;

public interface IAccountIdentityService
{
    Task<AuthenticatedUser?> CreateAuthenticatedUserAsync(
        CuentaAcceso account,
        CancellationToken cancellationToken = default);
}

public sealed class AccountIdentityService : IAccountIdentityService
{
    private readonly BioRedDbContext _dbContext;

    public AccountIdentityService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuthenticatedUser?> CreateAuthenticatedUserAsync(
        CuentaAcceso account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var userType = account.TipoUsuario.Trim();
        var referenceId = account.ReferenciaId.Trim();

        if (userType.Equals("Empleado", StringComparison.OrdinalIgnoreCase))
        {
            var employee = await _dbContext.Usuarios
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CodUsuario == referenceId && item.Estado,
                    cancellationToken);

            if (employee is null)
            {
                return null;
            }

            var roles = await _dbContext.Roles
                .AsNoTracking()
                .Where(item => item.CodRol == employee.CodRol && item.Estado)
                .Select(item => item.CodRol)
                .ToListAsync(cancellationToken);

            var permissions = await
            (
                from rolePermission in _dbContext.RolPermisos.AsNoTracking()
                join permission in _dbContext.Permisos.AsNoTracking()
                    on rolePermission.CodPermiso equals permission.CodPermiso
                where rolePermission.CodRol == employee.CodRol
                select permission.CodPermiso
            )
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);

            return new AuthenticatedUser(
                account.IdCuenta,
                employee.NombreUsuario,
                account.Correo,
                $"{employee.NombreUsuario} {employee.ApellidoUsuario}".Trim(),
                "Empleado",
                employee.CodUsuario,
                roles,
                permissions);
        }

        if (userType.Equals("Cliente", StringComparison.OrdinalIgnoreCase))
        {
            var client = await _dbContext.Clientes
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CodCliente == referenceId && item.Estado,
                    cancellationToken);

            return client is null
                ? null
                : new AuthenticatedUser(
                    account.IdCuenta,
                    account.Correo,
                    account.Correo,
                    $"{client.NombreCliente} {client.ApellidoCliente}".Trim(),
                    "Cliente",
                    client.CodCliente,
                    new[] { "CLIENTE" },
                    Array.Empty<string>());
        }

        if (userType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase))
        {
            var driver = await _dbContext.Repartidores
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CodRepartidor == referenceId && item.Estado,
                    cancellationToken);

            return driver is null
                ? null
                : new AuthenticatedUser(
                    account.IdCuenta,
                    account.Correo,
                    account.Correo,
                    $"{driver.NombreRepartidor} {driver.ApellidosRepartidor}".Trim(),
                    "Repartidor",
                    driver.CodRepartidor,
                    new[] { "REPARTIDOR" },
                    Array.Empty<string>());
        }

        return null;
    }
}
