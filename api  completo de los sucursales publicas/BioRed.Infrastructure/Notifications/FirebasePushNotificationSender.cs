using System.Text;
using BioRed.Application.Notifications;
using BioRed.Infrastructure.Persistence;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Notifications;

public sealed class FirebasePushNotificationSender : IPushNotificationSender
{
    private static readonly object AppLock = new();
    private static FirebaseApp? _firebaseApp;

    private readonly BioRedDbContext _dbContext;
    private readonly FirebaseOptions _options;
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    public FirebasePushNotificationSender(
        BioRedDbContext dbContext,
        IOptions<FirebaseOptions> options,
        ILogger<FirebasePushNotificationSender> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendToAccountAsync(
        int accountId,
        string type,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        List<DeviceToken> tokens;
        try
        {
            tokens = await _dbContext.DispositivosNotificacion
                    .Where(item => item.IdCuenta == accountId && item.Activo)
                    .Select(item => new DeviceToken(item.IdDispositivo, item.TokenDispositivo))
                    .ToListAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudieron consultar los dispositivos para la notificación {NotificationType}.",
                type);
            return;
        }

        if (tokens.Count == 0)
        {
            return;
        }

        FirebaseMessaging messaging;
        try
        {
            messaging = FirebaseMessaging.GetMessaging(GetOrCreateFirebaseApp());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "No se pudo inicializar Firebase para la notificación {NotificationType}.",
                type);
            return;
        }

        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = type
        };

        if (data is not null)
        {
            foreach (var item in data)
            {
                payload[item.Key] = item.Value;
            }
        }

        try
        {
            var response = await messaging.SendEachForMulticastAsync(
                new MulticastMessage
                {
                    Tokens = tokens.Select(item => item.TokenDispositivo).ToList(),
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = payload
                });

            var invalidDeviceIds = response.Responses
                .Select((item, index) => new { item, index })
                .Where(item =>
                    !item.item.IsSuccess &&
                    IsInvalidToken(item.item.Exception))
                .Select(item => tokens[item.index].IdDispositivo)
                .ToArray();

            if (invalidDeviceIds.Length > 0)
            {
                var invalidDevices = await _dbContext.DispositivosNotificacion
                    .Where(item => invalidDeviceIds.Contains(item.IdDispositivo))
                    .ToListAsync(cancellationToken);

                foreach (var device in invalidDevices)
                {
                    device.Activo = false;
                    device.FechaActualizacionUtc = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            // Firebase nunca debe impedir guardar pedidos o mensajes.
            _logger.LogWarning(
                exception,
                "Falló el envío de la notificación {NotificationType} para la cuenta {AccountId}.",
                type,
                accountId);
        }
    }

    private FirebaseApp GetOrCreateFirebaseApp()
    {
        if (_firebaseApp is not null)
        {
            return _firebaseApp;
        }

        lock (AppLock)
        {
            if (_firebaseApp is not null)
            {
                return _firebaseApp;
            }

            if (string.IsNullOrWhiteSpace(_options.ProjectId) ||
                string.IsNullOrWhiteSpace(_options.ServiceAccountJsonBase64))
            {
                throw new InvalidOperationException(
                    "Firebase está habilitado, pero faltan ProjectId o ServiceAccountJsonBase64.");
            }

            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(_options.ServiceAccountJsonBase64));
            var credential = GoogleCredential.FromJson(json);

            _firebaseApp = FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
                ProjectId = _options.ProjectId
            });

            return _firebaseApp;
        }
    }

    private static bool IsInvalidToken(Exception? exception)
    {
        var message = exception?.Message ?? string.Empty;
        return message.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("invalid-registration-token", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DeviceToken(long IdDispositivo, string TokenDispositivo);
}
