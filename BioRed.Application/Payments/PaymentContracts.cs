using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BioRed.Application.Payments;

public sealed class CreatePayPalOrderRequest
{
    [StringLength(100)]
    public string? RequestReference { get; init; }
}

public sealed class ConfirmCashPaymentRequest
{
    [StringLength(100)]
    public string? ExternalReference { get; init; }

    [StringLength(255)]
    public string? Observations { get; init; }
}

public sealed class ProcessRefundRequest
{
    [Range(1, int.MaxValue)]
    public int ReturnId { get; init; }

    [StringLength(100)]
    public string? ExternalReference { get; init; }

    [StringLength(255)]
    public string? Observations { get; init; }
}

public sealed record PaymentResponse(
    long PaymentId,
    int OrderId,
    string OrderCode,
    int? ReceivableId,
    string ClientCode,
    string PaymentTypeCode,
    string Provider,
    string Status,
    decimal LocalAmount,
    string LocalCurrency,
    decimal ProviderAmount,
    string ProviderCurrency,
    decimal ExchangeRate,
    string? ProviderOrderId,
    string? ProviderCaptureId,
    string? ApprovalUrl,
    string IdempotencyKey,
    int CreatedByAccountId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureDetail);

public sealed record GetOrderPaymentsResponse(
    int OrderId,
    string OrderCode,
    decimal OrderTotal,
    string PaymentTypeCode,
    bool IsPaid,
    IReadOnlyCollection<PaymentResponse> Payments);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReceiptDeliveryMethod
{
    [JsonStringEnumMemberName("DOWNLOAD_PDF")]
    DownloadPdf,

    [JsonStringEnumMemberName("EMAIL")]
    Email
}

public sealed class DeliverPaymentReceiptRequest
{
    [Required]
    public ReceiptDeliveryMethod? DeliveryMethod { get; init; }
}

public sealed record PaymentReceiptResponse(
    long ReceiptId,
    string ReceiptNumber,
    long PaymentId,
    int OrderId,
    string OrderCode,
    string ClientCode,
    string ClientName,
    string? RegisteredEmail,
    string PaymentTypeCode,
    string Provider,
    decimal PaidAmount,
    string Currency,
    DateTime PaidAtUtc,
    DateTime IssuedAtUtc,
    string EmailStatus,
    int EmailAttempts,
    DateTime? LastEmailAtUtc,
    IReadOnlyCollection<ReceiptDeliveryMethod> AvailableDeliveryMethods);

public sealed record PaymentReceiptFile(
    byte[] Content,
    string FileName);

public enum PaymentReceiptStatus
{
    Success,
    NotFound,
    Forbidden,
    PaymentNotCompleted,
    EmailNotConfigured,
    RegisteredEmailMissing,
    EmailDeliveryFailed
}

public sealed record PaymentReceiptResult(
    PaymentReceiptStatus Status,
    PaymentReceiptResponse? Receipt = null,
    PaymentReceiptFile? File = null,
    string? Detail = null);

public sealed record RefundResponse(
    long RefundId,
    int ReturnId,
    int InvoiceId,
    int OrderId,
    long? PaymentId,
    string Provider,
    string Status,
    decimal LocalAmount,
    string LocalCurrency,
    decimal ProviderAmount,
    string ProviderCurrency,
    decimal ExchangeRate,
    string? ProviderRefundId,
    string IdempotencyKey,
    string ProcessedBy,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? Observations,
    string? FailureDetail);

public enum PaymentManagementStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidPaymentType,
    InvalidOrderState,
    AlreadyPaid,
    InvalidAmount,
    ConfigurationError,
    ProviderError,
    RefundNotRequired,
    RefundAlreadyProcessed,
    CashReferenceRequired,
    StatusNotConfigured
}

public sealed record PaymentManagementResult(
    PaymentManagementStatus Status,
    PaymentResponse? Payment = null,
    GetOrderPaymentsResponse? OrderPayments = null,
    RefundResponse? Refund = null,
    string? Detail = null);

public sealed record PayPalOrderGatewayResult(
    bool Success,
    string? OrderId,
    string? Status,
    decimal Amount,
    string? Currency,
    string? ApprovalUrl,
    string? CaptureId,
    string? Error);

public sealed record PayPalRefundGatewayResult(
    bool Success,
    string? RefundId,
    string? Status,
    decimal Amount,
    string? Currency,
    string? Error);

public interface IPayPalGateway
{
    Task<PayPalOrderGatewayResult> CreateOrderAsync(
        decimal amount,
        string currency,
        string requestId,
        string description,
        CancellationToken cancellationToken = default);

    Task<PayPalOrderGatewayResult> CaptureOrderAsync(
        string orderId,
        string requestId,
        CancellationToken cancellationToken = default);

    Task<PayPalRefundGatewayResult> RefundCaptureAsync(
        string captureId,
        decimal amount,
        string currency,
        string requestId,
        string? note,
        CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<PaymentManagementResult> GetByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PaymentManagementResult> CreatePayPalOrderAsync(
        int orderId,
        string clientCode,
        int actorAccountId,
        CreatePayPalOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentManagementResult> CapturePayPalOrderAsync(
        string providerOrderId,
        string clientCode,
        CancellationToken cancellationToken = default);

    Task<PaymentManagementResult> ConfirmCashAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        int actorAccountId,
        bool isAdministrator,
        ConfirmCashPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentManagementResult> ProcessRefundAsync(
        string actorReferenceId,
        ProcessRefundRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPaymentReceiptService
{
    Task EnsureForPaymentAsync(
        long paymentId,
        CancellationToken cancellationToken = default);

    Task<PaymentReceiptResult> GetByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PaymentReceiptResult> DownloadByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PaymentReceiptResult> EmailByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}
