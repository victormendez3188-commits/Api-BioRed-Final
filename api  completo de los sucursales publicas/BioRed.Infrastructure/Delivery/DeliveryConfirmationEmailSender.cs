using System.Net;
using BioRed.Infrastructure.Payments;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BioRed.Infrastructure.Delivery;

/// <summary>
/// Uses the existing protected SMTP configuration. The code is only placed in
/// the email body and is never stored or written to logs.
/// </summary>
internal sealed class DeliveryConfirmationEmailSender
{
    private readonly ReceiptEmailOptions _options;

    public DeliveryConfirmationEmailSender(IOptions<ReceiptEmailOptions> options)
    {
        _options = options.Value;
    }

    public string? ValidateConfiguration()
    {
        if (!_options.Enabled)
        {
            return "El correo de BioRed está deshabilitado para enviar códigos de entrega.";
        }

        if (string.IsNullOrWhiteSpace(_options.Host) ||
            _options.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            return "Configure ReceiptEmail:Host, ReceiptEmail:Port y ReceiptEmail:FromAddress mediante secretos protegidos.";
        }

        if (!string.IsNullOrWhiteSpace(_options.UserName) &&
            string.IsNullOrWhiteSpace(_options.Password))
        {
            return "Configure ReceiptEmail:Password mediante secretos protegidos.";
        }

        return ResolveSecurity() is null
            ? "ReceiptEmail:Security debe ser Auto, StartTls, SslOnConnect o None."
            : null;
    }

    public async Task SendAsync(
        string destination,
        string clientName,
        string orderCode,
        string code,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var security = ResolveSecurity() ??
            throw new InvalidOperationException("La seguridad SMTP no es válida.");

        var safeClientName = WebUtility.HtmlEncode(clientName);
        var safeOrderCode = WebUtility.HtmlEncode(orderCode);
        var safeCode = WebUtility.HtmlEncode(code);
        var expiration = expiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'");

        var message = new MimeMessage
        {
            Subject = $"Código de entrega del pedido {orderCode}"
        };
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(_options.FromName) ? "BioRed" : _options.FromName,
            _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(destination));

        message.Body = new BodyBuilder
        {
            TextBody =
                $"Hola {clientName},\n\n" +
                $"Tu código de 4 dígitos para confirmar la entrega del pedido {orderCode} es: {code}\n" +
                $"Vence el {expiration}. Compártelo únicamente con el repartidor cuando te entregue el pedido.\n\nBioRed",
            HtmlBody =
                $"<p>Hola <strong>{safeClientName}</strong>,</p>" +
                $"<p>Tu código de 4 dígitos para confirmar la entrega del pedido <strong>{safeOrderCode}</strong> es:</p>" +
                $"<p style=\"font-size:28px;letter-spacing:6px\"><strong>{safeCode}</strong></p>" +
                $"<p>Vence el <strong>{expiration}</strong>. Compártelo únicamente con el repartidor cuando te entregue el pedido.</p>" +
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
        (_options.Security ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "AUTO" => SecureSocketOptions.Auto,
            "STARTTLS" => SecureSocketOptions.StartTls,
            "SSLONCONNECT" => SecureSocketOptions.SslOnConnect,
            "NONE" => SecureSocketOptions.None,
            _ => null
        };
}
