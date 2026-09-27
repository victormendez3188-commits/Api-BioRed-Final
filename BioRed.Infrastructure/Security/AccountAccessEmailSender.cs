using System.Net;
using BioRed.Infrastructure.Payments;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BioRed.Infrastructure.Security;

internal sealed class AccountAccessEmailSender
{
    private readonly ReceiptEmailOptions _options;

    public AccountAccessEmailSender(IOptions<ReceiptEmailOptions> options)
    {
        _options = options.Value;
    }

    public string? ValidateConfiguration()
    {
        if (!_options.Enabled)
        {
            return "El envío de correo está deshabilitado.";
        }

        if (string.IsNullOrWhiteSpace(_options.Host) ||
            _options.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            return "Configure ReceiptEmail:Host, ReceiptEmail:Port y ReceiptEmail:FromAddress mediante User Secrets.";
        }

        if (!string.IsNullOrWhiteSpace(_options.UserName) &&
            string.IsNullOrWhiteSpace(_options.Password))
        {
            return "Configure ReceiptEmail:Password mediante User Secrets.";
        }

        return ResolveSecurity() is null
            ? "ReceiptEmail:Security debe ser Auto, StartTls, SslOnConnect o None."
            : null;
    }

    public async Task SendTemporaryPasswordAsync(
        string destination,
        string displayName,
        string temporaryPassword,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var security = ResolveSecurity() ??
            throw new InvalidOperationException("La seguridad SMTP no es válida.");

        var safeName = WebUtility.HtmlEncode(displayName);
        var safePassword = WebUtility.HtmlEncode(temporaryPassword);
        var expirationText = expiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

        var message = new MimeMessage
        {
            Subject = "Contraseña temporal de acceso a BioRed"
        };
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(_options.FromName) ? "BioRed" : _options.FromName,
            _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(destination));

        message.Body = new BodyBuilder
        {
            TextBody =
                $"Hola {displayName},\n\n" +
                $"Tu contraseña temporal de BioRed es: {temporaryPassword}\n" +
                $"Vence el {expirationText}.\n\n" +
                "Debes cambiarla antes de iniciar sesión. No la compartas. " +
                "Si vence, solicita a administración una nueva contraseña temporal.\n\nBioRed",
            HtmlBody =
                $"<p>Hola <strong>{safeName}</strong>,</p>" +
                "<p>Tu contraseña temporal de acceso a BioRed es:</p>" +
                $"<p style=\"font-size:20px\"><strong>{safePassword}</strong></p>" +
                $"<p>Vence el <strong>{expirationText}</strong>.</p>" +
                "<p>Debes cambiarla antes de iniciar sesión. No la compartas. " +
                "Si vence, solicita a administración una nueva contraseña temporal.</p>" +
                "<p>BioRed</p>"
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            security,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            await client.AuthenticateAsync(
                _options.UserName,
                _options.Password,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private SecureSocketOptions? ResolveSecurity() =>
        _options.Security.Trim().ToUpperInvariant() switch
        {
            "AUTO" => SecureSocketOptions.Auto,
            "STARTTLS" => SecureSocketOptions.StartTls,
            "SSLONCONNECT" => SecureSocketOptions.SslOnConnect,
            "NONE" => SecureSocketOptions.None,
            _ => null
        };
}
