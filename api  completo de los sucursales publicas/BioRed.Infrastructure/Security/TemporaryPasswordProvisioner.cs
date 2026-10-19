using BioRed.Infrastructure.Persistence.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BioRed.Infrastructure.Security;

internal sealed record PreparedTemporaryPassword(
    string PlainText,
    DateTime ExpiresAtUtc);

internal sealed class TemporaryPasswordProvisioner
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly IPasswordHasher<CuentaAcceso> _passwordHasher;
    private readonly TemporaryPasswordGenerator _generator;
    private readonly AccountAccessEmailSender _emailSender;
    private readonly ILogger<TemporaryPasswordProvisioner> _logger;

    public TemporaryPasswordProvisioner(
        IPasswordHasher<CuentaAcceso> passwordHasher,
        TemporaryPasswordGenerator generator,
        AccountAccessEmailSender emailSender,
        ILogger<TemporaryPasswordProvisioner> logger)
    {
        _passwordHasher = passwordHasher;
        _generator = generator;
        _emailSender = emailSender;
        _logger = logger;
    }

    public string? ValidateEmailConfiguration() =>
        _emailSender.ValidateConfiguration();

    public PreparedTemporaryPassword Prepare(CuentaAcceso account)
    {
        var plainText = _generator.Generate();
        var expiresAtUtc = DateTime.UtcNow.Add(Lifetime);

        account.PasswordHash = _passwordHasher.HashPassword(account, plainText);
        account.RequiereCambioPassword = true;
        account.PasswordTemporalExpiraUtc = expiresAtUtc;
        account.PasswordActualizadaUtc = null;
        account.EstadoEnvioPasswordTemporal = "PENDIENTE";
        account.DetalleFalloPasswordTemporal = null;

        return new PreparedTemporaryPassword(plainText, expiresAtUtc);
    }

    public async Task<bool> SendAsync(
        CuentaAcceso account,
        string displayName,
        PreparedTemporaryPassword prepared,
        CancellationToken cancellationToken)
    {
        account.IntentosEnvioPasswordTemporal++;
        account.UltimoEnvioPasswordTemporalUtc = DateTime.UtcNow;

        try
        {
            await _emailSender.SendTemporaryPasswordAsync(
                account.Correo,
                displayName,
                prepared.PlainText,
                prepared.ExpiresAtUtc,
                cancellationToken);

            account.EstadoEnvioPasswordTemporal = "ENVIADO";
            account.DetalleFalloPasswordTemporal = null;
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            account.EstadoEnvioPasswordTemporal = "FALLIDO";
            account.DetalleFalloPasswordTemporal =
                "No fue posible enviar la contraseña temporal por correo.";

            _logger.LogError(
                exception,
                "Falló el envío de la contraseña temporal para la cuenta {AccountId}.",
                account.IdCuenta);
            return false;
        }
    }
}
