using System.Data;
using BioRed.Application.Clients;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Clients;

public sealed class ClientAddressService : IClientAddressService
{
    private readonly BioRedDbContext _dbContext;

    public ClientAddressService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetClientAddressesResult> GetAsync(
        string clientCode,
        CancellationToken cancellationToken = default)
    {
        if (!await ClientExistsAsync(clientCode, cancellationToken))
        {
            return new GetClientAddressesResult(
                ClientAddressStatus.ClientNotFound,
                Detail: "No se encontró el cliente autenticado.");
        }

        var addresses = await _dbContext.DireccionesCliente
            .AsNoTracking()
            .Where(item =>
                item.CodCliente == clientCode &&
                item.Estado)
            .OrderByDescending(item => item.EsPredeterminada == true)
            .ThenBy(item => item.NombreDireccion)
            .ThenBy(item => item.IdDireccion)
            .Select(item => new ClientAddressResponse(
                item.IdDireccion,
                item.NombreDireccion,
                item.DireccionCompleta,
                item.Referencia,
                item.Latitud,
                item.Longitud,
                item.EsPredeterminada == true,
                item.Estado))
            .ToListAsync(cancellationToken);

        return new GetClientAddressesResult(
            ClientAddressStatus.Success,
            addresses);
    }

    public async Task<ClientAddressResult> GetByIdAsync(
        string clientCode,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        var address = await _dbContext.DireccionesCliente
            .AsNoTracking()
            .Where(item =>
                item.IdDireccion == addressId &&
                item.CodCliente == clientCode &&
                item.Estado)
            .Select(item => new ClientAddressResponse(
                item.IdDireccion,
                item.NombreDireccion,
                item.DireccionCompleta,
                item.Referencia,
                item.Latitud,
                item.Longitud,
                item.EsPredeterminada == true,
                item.Estado))
            .SingleOrDefaultAsync(cancellationToken);

        return address is null
            ? AddressNotFound()
            : new ClientAddressResult(
                ClientAddressStatus.Success,
                address);
    }

    public async Task<ClientAddressResult> CreateAsync(
        string clientCode,
        SaveClientAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            if (!await ClientExistsAsync(clientCode, cancellationToken))
            {
                return new ClientAddressResult(
                    ClientAddressStatus.ClientNotFound,
                    Detail: "No se encontró el cliente autenticado.");
            }

            var activeAddresses = await _dbContext.DireccionesCliente
                .Where(item =>
                    item.CodCliente == clientCode &&
                    item.Estado)
                .ToListAsync(cancellationToken);

            var mustBeDefault =
                request.IsDefault ||
                !activeAddresses.Any(item => item.EsPredeterminada == true);

            if (mustBeDefault)
            {
                foreach (var currentAddress in activeAddresses)
                {
                    currentAddress.EsPredeterminada = false;
                }
            }

            var address = new DireccionCliente
            {
                CodCliente = clientCode,
                NombreDireccion = request.Name.Trim(),
                DireccionCompleta = request.FullAddress.Trim(),
                Referencia = NormalizeOptionalText(request.Reference),
                Latitud = request.Latitude,
                Longitud = request.Longitude,
                EsPredeterminada = mustBeDefault,
                Estado = true
            };

            _dbContext.DireccionesCliente.Add(address);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ClientAddressResult(
                ClientAddressStatus.Success,
                ToResponse(address));
        });
    }

    public async Task<ClientAddressResult> UpdateAsync(
        string clientCode,
        int addressId,
        SaveClientAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var activeAddresses = await _dbContext.DireccionesCliente
                .Where(item =>
                    item.CodCliente == clientCode &&
                    item.Estado)
                .OrderBy(item => item.IdDireccion)
                .ToListAsync(cancellationToken);

            var address = activeAddresses.SingleOrDefault(
                item => item.IdDireccion == addressId);

            if (address is null)
            {
                return AddressNotFound();
            }

            address.NombreDireccion = request.Name.Trim();
            address.DireccionCompleta = request.FullAddress.Trim();
            address.Referencia = NormalizeOptionalText(request.Reference);
            address.Latitud = request.Latitude;
            address.Longitud = request.Longitude;

            if (request.IsDefault)
            {
                SetOnlyDefault(activeAddresses, address.IdDireccion);
            }
            else if (address.EsPredeterminada == true)
            {
                var replacement = activeAddresses.FirstOrDefault(
                    item => item.IdDireccion != address.IdDireccion);

                if (replacement is null)
                {
                    address.EsPredeterminada = true;
                }
                else
                {
                    SetOnlyDefault(
                        activeAddresses,
                        replacement.IdDireccion);
                }
            }
            else
            {
                address.EsPredeterminada = false;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ClientAddressResult(
                ClientAddressStatus.Success,
                ToResponse(address));
        });
    }

    public async Task<ClientAddressResult> SetDefaultAsync(
        string clientCode,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var activeAddresses = await _dbContext.DireccionesCliente
                .Where(item =>
                    item.CodCliente == clientCode &&
                    item.Estado)
                .ToListAsync(cancellationToken);

            var address = activeAddresses.SingleOrDefault(
                item => item.IdDireccion == addressId);

            if (address is null)
            {
                return AddressNotFound();
            }

            SetOnlyDefault(activeAddresses, address.IdDireccion);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ClientAddressResult(
                ClientAddressStatus.Success,
                ToResponse(address));
        });
    }

    public async Task<ClientAddressStatus> DeactivateAsync(
        string clientCode,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var activeAddresses = await _dbContext.DireccionesCliente
                .Where(item =>
                    item.CodCliente == clientCode &&
                    item.Estado)
                .OrderBy(item => item.IdDireccion)
                .ToListAsync(cancellationToken);

            var address = activeAddresses.SingleOrDefault(
                item => item.IdDireccion == addressId);

            if (address is null)
            {
                return ClientAddressStatus.AddressNotFound;
            }

            address.Estado = false;
            address.EsPredeterminada = false;

            var replacement = activeAddresses.FirstOrDefault(
                item => item.IdDireccion != address.IdDireccion);

            if (replacement is not null &&
                !activeAddresses.Any(item =>
                    item.IdDireccion != address.IdDireccion &&
                    item.EsPredeterminada == true))
            {
                replacement.EsPredeterminada = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return ClientAddressStatus.Success;
        });
    }

    private Task<bool> ClientExistsAsync(
        string clientCode,
        CancellationToken cancellationToken) =>
        _dbContext.Clientes
            .AsNoTracking()
            .AnyAsync(item =>
                item.CodCliente == clientCode &&
                item.Estado,
                cancellationToken);

    private static void SetOnlyDefault(
        IEnumerable<DireccionCliente> addresses,
        int defaultAddressId)
    {
        foreach (var address in addresses)
        {
            address.EsPredeterminada =
                address.IdDireccion == defaultAddressId;
        }
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientAddressResponse ToResponse(
        DireccionCliente address) =>
        new(
            address.IdDireccion,
            address.NombreDireccion,
            address.DireccionCompleta,
            address.Referencia,
            address.Latitud,
            address.Longitud,
            address.EsPredeterminada == true,
            address.Estado);

    private static ClientAddressResult AddressNotFound() =>
        new(
            ClientAddressStatus.AddressNotFound,
            Detail: "No se encontró una dirección activa perteneciente al cliente.");
}
