using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BioRed.Application.Payments;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Payments;

public sealed class PayPalGateway : IPayPalGateway
{
    public const string HttpClientName = "PayPalSandbox";

    private readonly HttpClient _httpClient;
    private readonly PayPalOptions _options;

    public PayPalGateway(
        HttpClient httpClient,
        IOptions<PayPalOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PayPalOrderGatewayResult> CreateOrderAsync(
        decimal amount,
        string currency,
        string requestId,
        string description,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration();
        if (configurationError is not null)
        {
            return FailedOrder(configurationError);
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        if (token.Token is null)
        {
            return FailedOrder(token.Error ?? "No fue posible autenticar con PayPal.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri("/v2/checkout/orders"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        request.Content = JsonContent.Create(new
        {
            intent = "CAPTURE",
            application_context = new
            {
                brand_name = "BioRed",
                landing_page = "LOGIN",
                shipping_preference = "NO_SHIPPING",
                user_action = "PAY_NOW",
                return_url = _options.ReturnUrl,
                cancel_url = _options.CancelUrl
            },
            purchase_units = new[]
            {
                new
                {
                    reference_id = requestId,
                    description,
                    amount = new
                    {
                        currency_code = currency,
                        value = FormatMoney(amount)
                    }
                }
            }
        });

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return FailedOrder(ReadProviderError(payload, response.StatusCode));
        }

        return ParseOrder(payload, includeApprovalUrl: true);
    }

    public async Task<PayPalOrderGatewayResult> CaptureOrderAsync(
        string orderId,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration();
        if (configurationError is not null)
        {
            return FailedOrder(configurationError);
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        if (token.Token is null)
        {
            return FailedOrder(token.Error ?? "No fue posible autenticar con PayPal.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri($"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}/capture"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        request.Content = JsonContent.Create(new { });

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return ParseOrder(payload, includeApprovalUrl: false);
        }

        // Recuperación idempotente: si PayPal ya capturó la orden, se consulta
        // el estado definitivo antes de informar un error local.
        var reconciled = await GetOrderAsync(
            orderId,
            token.Token,
            cancellationToken);

        return reconciled.Success && reconciled.Status == "COMPLETED"
            ? reconciled
            : FailedOrder(ReadProviderError(payload, response.StatusCode));
    }

    public async Task<PayPalRefundGatewayResult> RefundCaptureAsync(
        string captureId,
        decimal amount,
        string currency,
        string requestId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidateConfiguration();
        if (configurationError is not null)
        {
            return FailedRefund(configurationError);
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        if (token.Token is null)
        {
            return FailedRefund(token.Error ?? "No fue posible autenticar con PayPal.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri($"/v2/payments/captures/{Uri.EscapeDataString(captureId)}/refund"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.TryAddWithoutValidation("PayPal-Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        var refundAmount = new
        {
            currency_code = currency,
            value = FormatMoney(amount)
        };
        request.Content = string.IsNullOrWhiteSpace(note)
            ? JsonContent.Create(new { amount = refundAmount })
            : JsonContent.Create(new
            {
                amount = refundAmount,
                note_to_payer = note.Trim()
            });

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return FailedRefund(ReadProviderError(payload, response.StatusCode));
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var refundId = GetString(root, "id");
            var status = GetString(root, "status");
            var amountElement = root.GetProperty("amount");
            var parsedAmount = ParseMoney(GetString(amountElement, "value"));
            var parsedCurrency = GetString(amountElement, "currency_code");

            if (refundId is null || status is null || parsedCurrency is null)
            {
                return FailedRefund("PayPal devolvió un reembolso incompleto.");
            }

            return new(
                true,
                refundId,
                status,
                parsedAmount,
                parsedCurrency,
                null);
        }
        catch (JsonException)
        {
            return FailedRefund("PayPal devolvió un JSON de reembolso inválido.");
        }
    }

    private async Task<PayPalOrderGatewayResult> GetOrderAsync(
        string orderId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUri($"/v2/checkout/orders/{Uri.EscapeDataString(orderId)}"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        return response.IsSuccessStatusCode
            ? ParseOrder(payload, includeApprovalUrl: false)
            : FailedOrder(ReadProviderError(payload, response.StatusCode));
    }

    private async Task<(string? Token, string? Error)> GetAccessTokenAsync(
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri("/v1/oauth2/token"));
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return (null, ReadProviderError(payload, response.StatusCode));
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var token = GetString(document.RootElement, "access_token");
            return string.IsNullOrWhiteSpace(token)
                ? (null, "PayPal no devolvió un token de acceso.")
                : (token, null);
        }
        catch (JsonException)
        {
            return (null, "PayPal devolvió una autenticación inválida.");
        }
    }

    private PayPalOrderGatewayResult ParseOrder(
        string payload,
        bool includeApprovalUrl)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var orderId = GetString(root, "id");
            var status = GetString(root, "status");
            string? approvalUrl = null;

            if (includeApprovalUrl && root.TryGetProperty("links", out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (GetString(link, "rel") is "approve" or "payer-action")
                    {
                        approvalUrl = GetString(link, "href");
                        break;
                    }
                }
            }

            var amount = 0m;
            string? currency = null;
            string? captureId = null;
            if (root.TryGetProperty("purchase_units", out var purchaseUnits) &&
                purchaseUnits.GetArrayLength() > 0)
            {
                var purchaseUnit = purchaseUnits[0];
                if (purchaseUnit.TryGetProperty("payments", out var payments) &&
                    payments.TryGetProperty("captures", out var captures) &&
                    captures.GetArrayLength() > 0)
                {
                    var capture = captures[0];
                    captureId = GetString(capture, "id");
                    if (capture.TryGetProperty("amount", out var capturedAmount))
                    {
                        amount = ParseMoney(GetString(capturedAmount, "value"));
                        currency = GetString(capturedAmount, "currency_code");
                    }
                }
                else if (purchaseUnit.TryGetProperty("amount", out var orderAmount))
                {
                    amount = ParseMoney(GetString(orderAmount, "value"));
                    currency = GetString(orderAmount, "currency_code");
                }
            }

            if (orderId is null || status is null || currency is null || amount <= 0m)
            {
                return FailedOrder("PayPal devolvió una orden incompleta.");
            }

            return new(
                true,
                orderId,
                status,
                amount,
                currency,
                approvalUrl,
                captureId,
                null);
        }
        catch (JsonException)
        {
            return FailedOrder("PayPal devolvió un JSON de orden inválido.");
        }
    }

    private string? ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            return "Configure PayPal:ClientId y PayPal:ClientSecret mediante User Secrets.";
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return "PayPal:BaseUrl debe ser una URL HTTPS válida.";
        }

        if (!IsValidRedirectUrl(_options.ReturnUrl) ||
            !IsValidRedirectUrl(_options.CancelUrl))
        {
            return "PayPal:ReturnUrl y PayPal:CancelUrl deben ser URL absolutas HTTP o HTTPS válidas.";
        }

        return null;
    }

    private static bool IsValidRedirectUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp ||
         uri.Scheme == Uri.UriSchemeHttps);

    private Uri BuildUri(string relativePath) =>
        new(new Uri(_options.BaseUrl.TrimEnd('/') + "/"), relativePath.TrimStart('/'));

    private static string FormatMoney(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static decimal ParseMoney(string? value) =>
        decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0m;

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string ReadProviderError(
        string payload,
        System.Net.HttpStatusCode statusCode)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var name = GetString(root, "name");
            var message = GetString(root, "message");
            var detail = string.Join(
                " ",
                new[] { name, message }
                    .Where(item => !string.IsNullOrWhiteSpace(item)));

            return LimitDetail(
                string.IsNullOrWhiteSpace(detail)
                    ? $"PayPal respondió HTTP {(int)statusCode}."
                    : $"PayPal respondió HTTP {(int)statusCode}: {detail}");
        }
        catch (JsonException)
        {
            return $"PayPal respondió HTTP {(int)statusCode}.";
        }
    }

    private static string LimitDetail(string detail) =>
        detail.Length <= 500 ? detail : detail[..500];

    private static PayPalOrderGatewayResult FailedOrder(string error) =>
        new(false, null, null, 0m, null, null, null, LimitDetail(error));

    private static PayPalRefundGatewayResult FailedRefund(string error) =>
        new(false, null, null, 0m, null, LimitDetail(error));
}
