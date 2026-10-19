using System.ComponentModel.DataAnnotations;

namespace BioRed.Application.Payments;

public sealed class UpsertDriverPayoutAccountRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string PayPalEmail { get; init; } = string.Empty;
}

public sealed class GetDriverSettlementsRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    [StringLength(15)]
    public string? DriverCode { get; init; }

    [StringLength(30)]
    public string? Status { get; init; }

    [StringLength(20)]
    public string? Source { get; init; }
}

public sealed class ConfirmDriverCashRemittanceRequest
{
    [Required]
    [StringLength(100)]
    public string Reference { get; init; } = string.Empty;

    [StringLength(300)]
    public string? Observations { get; init; }
}

public sealed class ConfirmDriverRefundRecoveryRequest
{
    [Required]
    [StringLength(100)]
    public string Reference { get; init; } = string.Empty;

    [StringLength(300)]
    public string? Observations { get; init; }
}

public sealed record DriverPayoutAccountResponse(
    string DriverCode,
    string Provider,
    string PayPalEmail,
    bool Active,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DriverSettlementResponse(
    long SettlementId,
    int OrderId,
    string OrderCode,
    string DriverCode,
    string DriverName,
    long? PaymentId,
    string Source,
    string Status,
    decimal DriverAmount,
    string LocalCurrency,
    decimal? ProviderAmount,
    string? ProviderCurrency,
    decimal? ExchangeRate,
    decimal CashCollected,
    decimal CashToRemit,
    string? PayPalEmail,
    string? ProviderBatchId,
    string? ProviderItemId,
    string? ProviderTransactionId,
    string IdempotencyKey,
    int Attempts,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CashReconciledAtUtc,
    string? CashReconciledBy,
    string? CashReconciliationReference,
    string? Observations,
    string? FailureDetail,
    decimal ReversedDriverAmount = 0m,
    decimal NetDriverAmount = 0m,
    string RecoveryStatus = "SIN_AJUSTE");

public sealed record DriverSettlementRefundAdjustmentResponse(
    long AdjustmentId,
    long SettlementId,
    int OrderId,
    string OrderCode,
    string DriverCode,
    string DriverName,
    long RefundId,
    string Type,
    string Status,
    string RecoveryMethod,
    decimal Amount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    string? ConfirmedBy,
    string? ConfirmationReference,
    string? Observations);

public sealed record GetDriverSettlementRefundAdjustmentsResponse(
    long SettlementId,
    IReadOnlyCollection<DriverSettlementRefundAdjustmentResponse> Items);

public sealed record GetDriverSettlementsResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    IReadOnlyCollection<DriverSettlementResponse> Items);

public enum DriverSettlementStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidState,
    DuplicatePayoutAccount,
    ConfigurationError,
    ProviderError
}

public sealed record DriverSettlementResult(
    DriverSettlementStatus Status,
    DriverPayoutAccountResponse? Account = null,
    DriverSettlementResponse? Settlement = null,
    GetDriverSettlementsResponse? Settlements = null,
    DriverSettlementRefundAdjustmentResponse? RefundAdjustment = null,
    GetDriverSettlementRefundAdjustmentsResponse? RefundAdjustments = null,
    string? Detail = null);

public sealed record PayPalPayoutGatewayResult(
    bool Success,
    string? BatchId,
    string? BatchStatus,
    string? ItemId,
    string? TransactionStatus,
    string? TransactionId,
    string? Error);

public interface IDriverSettlementService
{
    Task<DriverSettlementResult> GetPayoutAccountAsync(
        string driverCode,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> UpsertPayoutAccountAsync(
        string driverCode,
        UpsertDriverPayoutAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> GetAsync(
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        GetDriverSettlementsRequest request,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> GetByIdAsync(
        long settlementId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> EnsurePayPalSettlementAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> EnsureCashSettlementAsync(
        long paymentId,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> RetryPayPalAsync(
        long settlementId,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> RefreshPayPalAsync(
        long settlementId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> ConfirmCashRemittanceAsync(
        long settlementId,
        string administratorCode,
        ConfirmDriverCashRemittanceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra de forma idempotente la recuperación del pago de entrega
    /// cuando un reembolso incluye el costo de envío.
    /// </summary>
    Task<DriverSettlementResult> EnsureRefundRecoveryAsync(
        long refundId,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> GetRefundAdjustmentsAsync(
        long settlementId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<DriverSettlementResult> ConfirmRefundRecoveryAsync(
        long adjustmentId,
        string administratorCode,
        ConfirmDriverRefundRecoveryRequest request,
        CancellationToken cancellationToken = default);
}
