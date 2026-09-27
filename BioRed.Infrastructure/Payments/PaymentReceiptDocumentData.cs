namespace BioRed.Infrastructure.Payments;

internal sealed record PaymentReceiptLine(
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

internal sealed record PaymentReceiptDocumentData(
    long ReceiptId,
    string ReceiptNumber,
    long PaymentId,
    int OrderId,
    string OrderCode,
    string ClientCode,
    string ClientName,
    string? ClientTaxId,
    string BranchName,
    string BranchAddress,
    string CompanyName,
    string PaymentTypeCode,
    string Provider,
    string? ProviderReference,
    decimal Subtotal,
    decimal DeliveryCost,
    decimal Discount,
    decimal PaidAmount,
    string Currency,
    decimal RefundedAmount,
    DateTime PaidAtUtc,
    DateTime IssuedAtUtc,
    IReadOnlyCollection<PaymentReceiptLine> Items);
