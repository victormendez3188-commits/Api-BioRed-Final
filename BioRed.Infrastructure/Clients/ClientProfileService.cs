using System.Data;
using BioRed.Application.Clients;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using BioRed.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Clients;

internal sealed class ClientProfileService : IClientProfileService
{
    private readonly BioRedDbContext _dbContext;
    private readonly TemporaryPasswordProvisioner _temporaryPasswordProvisioner;

    public ClientProfileService(
        BioRedDbContext dbContext,
        TemporaryPasswordProvisioner temporaryPasswordProvisioner)
    {
        _dbContext = dbContext;
        _temporaryPasswordProvisioner = temporaryPasswordProvisioner;
    }

    public async Task<ClientProfileResult> RegisterAsync(
        RegisterClientRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var emailConfigurationError =
            _temporaryPasswordProvisioner.ValidateEmailConfiguration();

        if (emailConfigurationError is not null)
        {
            return Failure(
                ClientProfileStatus.EmailConfigurationInvalid,
                emailConfigurationError);
        }

        var branchCode = request.BranchCode.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        var document = request.Document.Trim().ToUpperInvariant();

        var branchExists = await _dbContext.Sucursales
            .AsNoTracking()
            .AnyAsync(
                item => item.CodSucursal == branchCode && item.Estado,
                cancellationToken);

        if (!branchExists)
        {
            return Failure(
                ClientProfileStatus.BranchNotFound,
                $"No existe la sucursal activa {branchCode}.");
        }

        var duplicateResult = await FindDuplicateAsync(
            email,
            document,
            cancellationToken);

        if (duplicateResult is not null)
        {
            return duplicateResult;
        }

        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

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
                    cancellationToken);

                if (duplicateInsideTransaction is not null)
                {
                    return duplicateInsideTransaction;
                }

                var creatorUserCode = await _dbContext.Usuarios
                    .AsNoTracking()
                    .Where(item => item.Estado)
                    .OrderByDescending(item => item.CodSucursal == branchCode)
                    .ThenBy(item => item.CodUsuario)
                    .Select(item => item.CodUsuario)
                    .FirstOrDefaultAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(creatorUserCode))
                {
                    return Failure(
                        ClientProfileStatus.SystemUserNotFound,
                        "No existe un usuario interno activo para registrar al cliente.");
                }

                var clientCode = await CreateClientCodeAsync(cancellationToken);
                var createdAtUtc = DateTime.UtcNow;

                var client = new Cliente
                {
                    CodCliente = clientCode,
                    CodSucursal = branchCode,
                    NombreCliente = request.FirstName.Trim(),
                    ApellidoCliente = request.LastName.Trim(),
                    CuiNit = document,
                    TelefonoCliente = CleanOptional(request.Phone),
                    EmailCliente = email,
                    Estado = true,
                    CreadoEl = createdAtUtc,
                    CreadoPor = creatorUserCode
                };

                var account = new CuentaAcceso
                {
                    Correo = email,
                    TipoUsuario = "Cliente",
                    ReferenciaId = clientCode,
                    Estado = true,
                    CreadoEl = createdAtUtc
                };

                var preparedPassword =
                    _temporaryPasswordProvisioner.Prepare(account);

                _dbContext.Clientes.Add(client);
                _dbContext.CuentasAcceso.Add(account);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var emailSent =
                    await _temporaryPasswordProvisioner.SendAsync(
                        account,
                        $"{client.NombreCliente} {client.ApellidoCliente}",
                        preparedPassword,
                        cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (!emailSent)
                {
                    return Failure(
                        ClientProfileStatus.EmailDeliveryFailed,
                        "El cliente fue creado, pero no se pudo enviar la contraseña temporal. Administración debe reenviarla.");
                }

                return new ClientProfileResult(
                    ClientProfileStatus.Success,
                    Map(
                        client,
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
                ClientProfileStatus.DuplicateData,
                "El correo o documento ya está registrado.");
        }
        catch (DbUpdateException exception)
            when (IsForeignKeyConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                ClientProfileStatus.SystemUserNotFound,
                "No fue posible asociar el registro a un usuario interno activo.");
        }
    }

    public async Task<ClientProfileResult> GetAsync(
        string clientCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = clientCode.Trim().ToUpperInvariant();

        var profile = await
        (
            from client in _dbContext.Clientes.AsNoTracking()
            join account in _dbContext.CuentasAcceso.AsNoTracking()
                on client.CodCliente equals account.ReferenciaId
            where client.CodCliente == normalizedCode &&
                  client.Estado &&
                  account.TipoUsuario == "Cliente" &&
                  account.Estado
            select new ClientProfileResponse(
                client.CodCliente,
                client.CodSucursal,
                client.NombreCliente,
                client.ApellidoCliente,
                client.CuiNit,
                client.TelefonoCliente,
                client.EmailCliente ?? account.Correo,
                client.Estado,
                client.CreadoEl,
                false,
                null,
                null)
        )
        .FirstOrDefaultAsync(cancellationToken);

        return profile is null
            ? NotFound()
            : new(ClientProfileStatus.Success, profile);
    }

    public async Task<ClientProfileResult> UpdateAsync(
        string clientCode,
        UpdateClientProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = clientCode.Trim().ToUpperInvariant();
        var client = await _dbContext.Clientes
            .SingleOrDefaultAsync(
                item => item.CodCliente == normalizedCode && item.Estado,
                cancellationToken);

        if (client is null)
        {
            return NotFound();
        }

        var email = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .Where(item =>
                item.TipoUsuario == "Cliente" &&
                item.ReferenciaId == normalizedCode &&
                item.Estado)
            .Select(item => item.Correo)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(email))
        {
            return NotFound();
        }

        client.NombreCliente = request.FirstName.Trim();
        client.ApellidoCliente = request.LastName.Trim();
        client.TelefonoCliente = CleanOptional(request.Phone);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(
            ClientProfileStatus.Success,
            Map(client, email));
    }

    private async Task<ClientProfileResult?> FindDuplicateAsync(
        string email,
        string document,
        CancellationToken cancellationToken)
    {
        var emailExists = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .AnyAsync(item => item.Correo == email, cancellationToken) ||
            await _dbContext.Clientes
                .AsNoTracking()
                .AnyAsync(item => item.EmailCliente == email, cancellationToken);

        if (emailExists)
        {
            return Failure(
                ClientProfileStatus.DuplicateEmail,
                "Ya existe una cuenta con ese correo electrónico.");
        }

        var documentExists = await _dbContext.Clientes
            .AsNoTracking()
            .AnyAsync(item => item.CuiNit == document, cancellationToken);

        return documentExists
            ? Failure(
                ClientProfileStatus.DuplicateDocument,
                "Ya existe un cliente con ese documento.")
            : null;
    }

    private async Task<string> CreateClientCodeAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"CLI-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
            var exists = await _dbContext.Clientes
                .AsNoTracking()
                .AnyAsync(item => item.CodCliente == code, cancellationToken);

            if (!exists)
            {
                return code;
            }
        }

        throw new InvalidOperationException(
            "No fue posible generar un código único para el cliente.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private static bool IsForeignKeyConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number == 547;

    private static ClientProfileResponse Map(
        Cliente client,
        string email,
        bool passwordChangeRequired = false,
        DateTime? temporaryPasswordExpiresAtUtc = null,
        string? temporaryPasswordEmailStatus = null) =>
        new(
            client.CodCliente,
            client.CodSucursal,
            client.NombreCliente,
            client.ApellidoCliente,
            client.CuiNit,
            client.TelefonoCliente,
            email,
            client.Estado,
            client.CreadoEl,
            passwordChangeRequired,
            temporaryPasswordExpiresAtUtc,
            temporaryPasswordEmailStatus);

    private static ClientProfileResult NotFound() =>
        Failure(
            ClientProfileStatus.NotFound,
            "No existe un perfil activo para el cliente autenticado.");

    private static ClientProfileResult Failure(
        ClientProfileStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
