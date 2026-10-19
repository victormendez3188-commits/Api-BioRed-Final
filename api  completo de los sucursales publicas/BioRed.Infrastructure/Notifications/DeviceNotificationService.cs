using BioRed.Application.Notifications;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Notifications;

public sealed class DeviceNotificationService : IDeviceNotificationService
{
    private readonly BioRedDbContext _dbContext;

    public DeviceNotificationService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NotificationDeviceResponse> RegisterAsync(
        int accountId,
        RegisterNotificationDeviceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = request.Token.Trim();
        var platform = request.Platform.Trim();
        var nowUtc = DateTime.UtcNow;
        var device = await _dbContext.DispositivosNotificacion
            .SingleOrDefaultAsync(
                item => item.TokenDispositivo == token,
                cancellationToken);

        if (device is null)
        {
            device = new DispositivoNotificacion
            {
                IdCuenta = accountId,
                TokenDispositivo = token,
                Plataforma = platform,
                NombreDispositivo = CleanOptional(request.DeviceName),
                Activo = true,
                FechaRegistroUtc = nowUtc,
                FechaActualizacionUtc = nowUtc,
                UltimaConexionUtc = nowUtc
            };

            _dbContext.DispositivosNotificacion.Add(device);
        }
        else
        {
            // Un token puede cambiar de usuario al reinstalar o cambiar de sesión.
            device.IdCuenta = accountId;
            device.Plataforma = platform;
            device.NombreDispositivo = CleanOptional(request.DeviceName);
            device.Activo = true;
            device.FechaActualizacionUtc = nowUtc;
            device.UltimaConexionUtc = nowUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationDeviceResponse(
            device.IdDispositivo,
            device.Plataforma,
            device.NombreDispositivo,
            device.Activo,
            device.FechaRegistroUtc,
            device.FechaActualizacionUtc,
            device.UltimaConexionUtc);
    }

    public async Task UnregisterAsync(
        int accountId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var normalizedToken = token.Trim();
        if (normalizedToken.Length == 0)
        {
            return;
        }

        var device = await _dbContext.DispositivosNotificacion
            .SingleOrDefaultAsync(
                item => item.IdCuenta == accountId &&
                        item.TokenDispositivo == normalizedToken,
                cancellationToken);

        if (device is null)
        {
            return;
        }

        device.Activo = false;
        device.FechaActualizacionUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
