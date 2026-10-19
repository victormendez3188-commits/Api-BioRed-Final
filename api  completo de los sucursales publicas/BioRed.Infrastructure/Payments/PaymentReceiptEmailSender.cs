using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BioRed.Infrastructure.Payments;

internal sealed class PaymentReceiptEmailSender
{
    private readonly ReceiptEmailOptions _options;

    public PaymentReceiptEmailSender(IOptions<ReceiptEmailOptions> options)
    {
        _options = options.Value;
    }

    public string? ValidateConfiguration()
    {
        if (!_options.Enabled)
        {
            return "El envío de comprobantes por correo está deshabilitado.";
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

    public async Task SendAsync(
        string destination,
        string clientName,
        string receiptNumber,
        string orderCode,
        byte[] pdf,
        CancellationToken cancellationToken)
    {
        var security = ResolveSecurity() ??
            throw new InvalidOperationException("La seguridad SMTP no es válida.");

        var message = new MimeMessage
        {
            Subject = $"Comprobante de pago {receiptNumber}"
        };
        message.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(_options.FromName) ? "BioRed" : _options.FromName,
            _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(destination));

        var safeClientName = WebUtility.HtmlEncode(clientName);
        var safeReceiptNumber = WebUtility.HtmlEncode(receiptNumber);
        var safeOrderCode = WebUtility.HtmlEncode(orderCode);
        var body = new BodyBuilder
        {
            TextBody =
                $"Hola {clientName},\n\nAdjuntamos el comprobante {receiptNumber} correspondiente al pedido {orderCode}.\n\nBioRed",
            HtmlBody =
                $"<p>Hola <strong>{safeClientName}</strong>,</p>" +
                $"<p>Adjuntamos el comprobante <strong>{safeReceiptNumber}</strong> " +
                $"correspondiente al pedido <strong>{safeOrderCode}</strong>.</p>" +
                "<p>BioRed</p>"
        };
        body.Attachments.Add(
            $"{receiptNumber}.pdf",
            pdf,
            ContentType.Parse("application/pdf"));
        message.Body = body.ToMessageBody();

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
