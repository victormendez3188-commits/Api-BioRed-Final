namespace BioRed.Infrastructure.Payments;

public sealed class PayPalOptions
{
    public const string SectionName = "PayPal";

    public string BaseUrl { get; init; } = "https://api-m.sandbox.paypal.com";
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } =
        "https://localhost:7248/api/v1/payments/paypal/return";
    public string CancelUrl { get; init; } =
        "https://localhost:7248/api/v1/payments/paypal/cancel";
    public string LocalCurrency { get; init; } = "GTQ";
    public string PayPalCurrency { get; init; } = "USD";
    public decimal LocalCurrencyUnitsPerPayPalUnit { get; init; } = 7.70m;
}
