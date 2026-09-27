using BioRed.Application.Security;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Security;

internal sealed class AccountManagementService : IAccountManagementService
{
    private readonly BioRedDbContext _dbContext;
    private readonly TemporaryPasswordProvisioner _temporaryPasswordProvisioner;

    public AccountManagementService(
        BioRedDbContext dbContext,
        TemporaryPasswordProvisioner temporaryPasswordProvisioner)
    {
        _dbContext = dbContext;
        _temporaryPasswordProvisioner = temporaryPasswordProvisioner;
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        if (_temporaryPasswordProvisioner.ValidateEmailConfiguration() is not null) return;
        var normalized = email.Trim().ToLowerInvariant();
        var accountId = await _dbContext.CuentasAcceso.AsNoTracking()
            .Where(item => item.Correo == normalized && item.Estado)
            .Select(item => (int?)item.IdCuenta)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountId.HasValue)
            await ReissueTemporaryPasswordAsync(accountId.Value, cancellationToken);
    }

    public async Task<CreateAccountResult> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_temporaryPasswordProvisioner.ValidateEmailConfiguration() is not null)
        {
            return new CreateAccountResult(
                CreateAccountStatus.EmailConfigurationInvalid);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var userType = request.UserType.Trim();
        var referenceId = request.ReferenceId.Trim().ToUpperInvariant();

        var duplicate = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .AnyAsync(
                item => item.Correo == email ||
                        (item.TipoUsuario == userType && item.ReferenciaId == referenceId),
                cancellationToken);

        if (duplicate)
        {
            return new CreateAccountResult(CreateAccountStatus.Duplicate);
        }

        if (!await ReferenceExistsAsync(userType, referenceId, cancellationToken))
        {
            return new CreateAccountResult(CreateAccountStatus.ReferenceNotFound);
        }

        var createdAtUtc = DateTime.UtcNow;
        var account = new CuentaAcceso
        {
            Correo = email,
            TipoUsuario = userType,
            ReferenciaId = referenceId,
            Estado = true,
            CreadoEl = createdAtUtc
        };

        var preparedPassword = _temporaryPasswordProvisioner.Prepare(account);

        if (userType == "Empleado")
        {
            var employee = await _dbContext.Usuarios.SingleAsync(
                item => item.CodUsuario == referenceId,
                cancellationToken);
            employee.PrimerCambio = true;
        }

        _dbContext.CuentasAcceso.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var displayName = await ResolveDisplayNameAsync(
            userType,
            referenceId,
            cancellationToken);
        var emailSent = await _temporaryPasswordProvisioner.SendAsync(
            account,
            displayName,
            preparedPassword,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateAccountResult(
            emailSent
                ? CreateAccountStatus.Success
                : CreateAccountStatus.EmailDeliveryFailed,
            Map(account));
    }

    public async Task<CreateAccountResult> ReissueTemporaryPasswordAsync(
        int accountId,
        CancellationToken cancellationToken = default)
    {
        if (_temporaryPasswordProvisioner.ValidateEmailConfiguration() is not null)
        {
            return new CreateAccountResult(
                CreateAccountStatus.EmailConfigurationInvalid);
        }

        var account = await _dbContext.CuentasAcceso
            .SingleOrDefaultAsync(
                item => item.IdCuenta == accountId,
                cancellationToken);

        if (account is null)
        {
            return new CreateAccountResult(CreateAccountStatus.NotFound);
        }

        account.Estado = true;
        var preparedPassword = _temporaryPasswordProvisioner.Prepare(account);

        if (account.TipoUsuario == "Empleado")
        {
            var employee = await _dbContext.Usuarios.SingleOrDefaultAsync(
                item => item.CodUsuario == account.ReferenciaId,
                cancellationToken);
            if (employee is not null)
            {
                employee.PrimerCambio = true;
            }
        }

        var activeTokens = await _dbContext.RefreshTokens
            .Where(item => item.IdCuenta == account.IdCuenta && !item.Revocado)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revocado = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var displayName = await ResolveDisplayNameAsync(
            account.TipoUsuario,
            account.ReferenciaId,
            cancellationToken);
        var emailSent = await _temporaryPasswordProvisioner.SendAsync(
            account,
            displayName,
            preparedPassword,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateAccountResult(
            emailSent
                ? CreateAccountStatus.Success
                : CreateAccountStatus.EmailDeliveryFailed,
            Map(account));
    }

    private async Task<bool> ReferenceExistsAsync(
        string userType,
        string referenceId,
        CancellationToken cancellationToken) =>
        userType switch
        {
            "Empleado" => await _dbContext.Usuarios.AsNoTracking()
                .AnyAsync(item => item.CodUsuario == referenceId && item.Estado, cancellationToken),
            "Cliente" => await _dbContext.Clientes.AsNoTracking()
                .AnyAsync(item => item.CodCliente == referenceId && item.Estado, cancellationToken),
            "Repartidor" => await _dbContext.Repartidores.AsNoTracking()
                .AnyAsync(item => item.CodRepartidor == referenceId && item.Estado, cancellationToken),
            _ => false
        };

    private async Task<string> ResolveDisplayNameAsync(
        string userType,
        string referenceId,
        CancellationToken cancellationToken)
    {
        var displayName = userType switch
        {
            "Empleado" => await _dbContext.Usuarios.AsNoTracking()
                .Where(item => item.CodUsuario == referenceId)
                .Select(item => item.NombreUsuario + " " + item.ApellidoUsuario)
                .FirstOrDefaultAsync(cancellationToken),
            "Cliente" => await _dbContext.Clientes.AsNoTracking()
                .Where(item => item.CodCliente == referenceId)
                .Select(item => item.NombreCliente + " " + item.ApellidoCliente)
                .FirstOrDefaultAsync(cancellationToken),
            "Repartidor" => await _dbContext.Repartidores.AsNoTracking()
                .Where(item => item.CodRepartidor == referenceId)
                .Select(item => item.NombreRepartidor + " " + item.ApellidosRepartidor)
                .FirstOrDefaultAsync(cancellationToken),
            _ => null
        };

        return string.IsNullOrWhiteSpace(displayName)
            ? "Usuario BioRed"
            : displayName.Trim();
    }

    private static AccountResponse Map(CuentaAcceso account) =>
        new(
            account.IdCuenta,
            account.Correo,
            account.TipoUsuario,
            account.ReferenciaId,
            account.Estado,
            account.CreadoEl,
            account.RequiereCambioPassword,
            account.PasswordTemporalExpiraUtc,
            account.EstadoEnvioPasswordTemporal);
}
