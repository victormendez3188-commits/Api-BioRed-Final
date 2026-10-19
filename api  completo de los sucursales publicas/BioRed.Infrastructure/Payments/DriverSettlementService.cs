using System.Globalization;
using BioRed.Application.Notifications;
using BioRed.Application.Payments;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Payments;

public sealed class DriverSettlementService : IDriverSettlementService
{
    private const string PayPalSource = "PAYPAL";
    private const string CashSource = "CASH";
    private const string PendingConfiguration = "PENDIENTE_CONFIGURACION";
    private const string Pending = "PENDIENTE";
    private const string Processing = "PROCESANDO";
    private const string Completed = "COMPLETADO";
    private const string Error = "ERROR";
    private const string CashCollected = "EFECTIVO_COBRADO";
    private const string CashReconciled = "EFECTIVO_CONCILIADO";
    private const string RefundRecoveryPending = "PENDIENTE_RECUPERACION";
    private const string RefundRecoveryAppliedToCashRemittance = "APLICADO_A_REMESA";
    private const string RefundRecoveryRecovered = "RECUPERADO";
    private const string FutureRecovery = "RECUPERACION_FUTURA";
    private const string CashRemittanceRetention = "RETENCION_EN_REMESA";
    private const string PayPalPayoutRetention = "RETENCION_ANTES_DE_PAGO";
    private const string PayPalPayoutHeld = "RETENIDO_POR_REEMBOLSO";
    private const string RefundRecoveryType = "REVERSO_REEMBOLSO";

    private readonly BioRedDbContext _dbContext;
    private readonly IPayPalGateway _payPalGateway;
    private readonly IPushNotificationSender _pushSender;
    private readonly DriverSettlementOptions _options;
    private readonly PayPalOptions _payPalOptions;
    private readonly ILogger<DriverSettlementService> _logger;

    public DriverSettlementService(
        BioRedDbContext dbContext,
        IPayPalGateway payPalGateway,
        IPushNotificationSender pushSender,
        IOptions<DriverSettlementOptions> options,
        IOptions<PayPalOptions> payPalOptions,
        ILogger<DriverSettlementService> logger)
    {
        _dbContext = dbContext;
        _payPalGateway = payPalGateway;
        _pushSender = pushSender;
        _options = options.Value;
        _payPalOptions = payPalOptions.Value;
        _logger = logger;
    }

    public async Task<DriverSettlementResult> GetPayoutAccountAsync(
        string driverCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedDriver = NormalizeCode(driverCode);
        var account = await _dbContext.CuentasPagoRepartidor
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CodRepartidor == normalizedDriver,
                cancellationToken);

        return account is null
            ? Failure(
                DriverSettlementStatus.NotFound,
                "El repartidor todavía no configuró una cuenta PayPal para recibir liquidaciones.")
            : new(
                DriverSettlementStatus.Success,
                Account: MapAccount(account));
    }

    public async Task<DriverSettlementResult> UpsertPayoutAccountAsync(
        string driverCode,
        UpsertDriverPayoutAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedDriver = NormalizeCode(driverCode);
        var normalizedEmail = request.PayPalEmail.Trim().ToLowerInvariant();
        var driverExists = await _dbContext.Repartidores
            .AsNoTracking()
            .AnyAsync(
                item => item.CodRepartidor == normalizedDriver && item.Estado,
                cancellationToken);

        if (!driverExists)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró un repartidor activo.");
        }

        var emailBelongsToAnotherDriver = await _dbContext.CuentasPagoRepartidor
            .AsNoTracking()
            .AnyAsync(
                item => item.CorreoPaypal == normalizedEmail &&
                        item.CodRepartidor != normalizedDriver,
                cancellationToken);
        if (emailBelongsToAnotherDriver)
        {
            return Failure(
                DriverSettlementStatus.DuplicatePayoutAccount,
                "La cuenta PayPal ya está asociada con otro repartidor.");
        }

        var nowUtc = DateTime.UtcNow;
        var account = await _dbContext.CuentasPagoRepartidor
            .SingleOrDefaultAsync(
                item => item.CodRepartidor == normalizedDriver,
                cancellationToken);

        if (account is null)
        {
            account = new CuentaPagoRepartidor
            {
                CodRepartidor = normalizedDriver,
                Proveedor = PayPalSource,
                CorreoPaypal = normalizedEmail,
                Activa = true,
                CreadoElUtc = nowUtc,
                ActualizadoElUtc = nowUtc
            };
            _dbContext.CuentasPagoRepartidor.Add(account);
        }
        else
        {
            account.Proveedor = PayPalSource;
            account.CorreoPaypal = normalizedEmail;
            account.Activa = true;
            account.ActualizadoElUtc = nowUtc;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Failure(
                DriverSettlementStatus.DuplicatePayoutAccount,
                "La cuenta PayPal ya está asociada con otro repartidor.");
        }

        if (_options.Enabled)
        {
            var pendingIds = await _dbContext.LiquidacionesRepartidor
                .AsNoTracking()
                .Where(item =>
                    item.CodRepartidor == normalizedDriver &&
                    item.MetodoOrigen == PayPalSource &&
                    item.Estado == PendingConfiguration)
                .OrderBy(item => item.CreadoElUtc)
                .Select(item => item.IdLiquidacion)
                .Take(20)
                .ToArrayAsync(cancellationToken);

            foreach (var settlementId in pendingIds)
            {
                try
                {
                    await ProcessPayPalAsync(settlementId, cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "No se pudo procesar automáticamente la liquidación {SettlementId} después de configurar la cuenta.",
                        settlementId);
                }
            }
        }

        return new(
            DriverSettlementStatus.Success,
            Account: MapAccount(account));
    }

    public async Task<DriverSettlementResult> GetAsync(
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        GetDriverSettlementsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isDriver = actorType.Equals(
            "Repartidor",
            StringComparison.OrdinalIgnoreCase);
        if (!isAdministrator && !isDriver)
        {
            return Failure(
                DriverSettlementStatus.Forbidden,
                "La cuenta no puede consultar liquidaciones de repartidores.");
        }

        var query = _dbContext.LiquidacionesRepartidor.AsNoTracking();
        if (isDriver && !isAdministrator)
        {
            var driverCode = NormalizeCode(actorReferenceId);
            query = query.Where(item => item.CodRepartidor == driverCode);
        }
        else if (!string.IsNullOrWhiteSpace(request.DriverCode))
        {
            var driverCode = NormalizeCode(request.DriverCode);
            query = query.Where(item => item.CodRepartidor == driverCode);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToUpperInvariant();
            query = query.Where(item => item.Estado == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            var source = request.Source.Trim().ToUpperInvariant();
            query = query.Where(item => item.MetodoOrigen == source);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / request.PageSize);
        var rowsToSkip = (request.Page - 1) * request.PageSize;
        // Ordenar y paginar sobre la entidad. Hacerlo después de proyectar al
        // record de respuesta impide que EF Core traduzca la consulta a SQL.
        var pageQuery = query
            .OrderByDescending(item => item.CreadoElUtc)
            .ThenByDescending(item => item.IdLiquidacion)
            .Skip(rowsToSkip)
            .Take(request.PageSize);

        var items = await BuildResponseQuery(pageQuery)
            .ToArrayAsync(cancellationToken);

        return new(
            DriverSettlementStatus.Success,
            Settlements: new GetDriverSettlementsResponse(
                request.Page,
                request.PageSize,
                totalItems,
                totalPages,
                items));
    }

    public async Task<DriverSettlementResult> GetByIdAsync(
        long settlementId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var settlement = await BuildResponseQuery(
                _dbContext.LiquidacionesRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdLiquidacion == settlementId))
            .SingleOrDefaultAsync(cancellationToken);

        if (settlement is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró la liquidación.");
        }

        if (!CanAccess(
                settlement.DriverCode,
                actorType,
                actorReferenceId,
                isAdministrator))
        {
            return Failure(
                DriverSettlementStatus.Forbidden,
                "La cuenta no puede consultar esta liquidación.");
        }

        return new(
            DriverSettlementStatus.Success,
            Settlement: settlement);
    }

    public async Task<DriverSettlementResult> EnsurePayPalSettlementAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Pedidos
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdPedido == orderId,
                cancellationToken);
        if (order is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró el pedido.");
        }

        if (order.CodTipoPago != "PAGO-PAYPAL" ||
            order.EstadoPedido != "Entregado" ||
            string.IsNullOrWhiteSpace(order.CodRepartidor))
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "La liquidación PayPal requiere un pedido entregado y un repartidor asignado.");
        }

        if (order.PagoRepartidor <= 0m)
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "El pedido no contiene un monto válido para pagar al repartidor.");
        }

        var existing = await _dbContext.LiquidacionesRepartidor
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdPedido == order.IdPedido,
                cancellationToken);
        if (existing is not null)
        {
            return await GetInternalByIdAsync(
                existing.IdLiquidacion,
                cancellationToken);
        }

        var payment = await _dbContext.PagosPedidos
            .AsNoTracking()
            .Where(item =>
                item.IdPedido == order.IdPedido &&
                item.ProveedorPago == PayPalSource &&
                item.Estado == "COMPLETED")
            .OrderByDescending(item => item.IdPago)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "El pedido no tiene una captura PayPal completada.");
        }

        var payoutAccount = await _dbContext.CuentasPagoRepartidor
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.CodRepartidor == order.CodRepartidor &&
                    item.Activa,
                cancellationToken);
        var conversion = CalculateProviderAmount(order.PagoRepartidor);
        var detail = GetPayPalConfigurationDetail(payoutAccount, conversion);
        var settlement = new LiquidacionRepartidor
        {
            IdPedido = order.IdPedido,
            CodRepartidor = order.CodRepartidor,
            IdPago = payment.IdPago,
            MetodoOrigen = PayPalSource,
            Estado = detail is null ? Pending : PendingConfiguration,
            MontoRepartidorLocal = order.PagoRepartidor,
            MonedaLocal = NormalizeCurrency(_payPalOptions.LocalCurrency),
            MontoProveedor = conversion.Amount,
            MonedaProveedor = NormalizeCurrency(_payPalOptions.PayPalCurrency),
            TipoCambio = conversion.ExchangeRate,
            CorreoPaypalDestino = payoutAccount?.CorreoPaypal,
            ClaveIdempotencia = $"BIORED-DRV-{order.IdPedido}",
            CreadoElUtc = DateTime.UtcNow,
            DetalleFallo = detail
        };

        _dbContext.LiquidacionesRepartidor.Add(settlement);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            var concurrentId = await _dbContext.LiquidacionesRepartidor
                .AsNoTracking()
                .Where(item => item.IdPedido == order.IdPedido)
                .Select(item => item.IdLiquidacion)
                .SingleAsync(cancellationToken);
            return await GetInternalByIdAsync(concurrentId, cancellationToken);
        }

        return detail is null && _options.AutoProcessPayPalOnDelivery
            ? await ProcessPayPalAsync(settlement.IdLiquidacion, cancellationToken)
            : await GetInternalByIdAsync(
                settlement.IdLiquidacion,
                cancellationToken,
                detail is null
                    ? null
                    : DriverSettlementStatus.ConfigurationError);
    }

    public async Task<DriverSettlementResult> EnsureCashSettlementAsync(
        long paymentId,
        CancellationToken cancellationToken = default)
    {
        var data = await
        (
            from payment in _dbContext.PagosPedidos.AsNoTracking()
            join order in _dbContext.Pedidos.AsNoTracking()
                on payment.IdPedido equals order.IdPedido
            where payment.IdPago == paymentId
            select new { Payment = payment, Order = order }
        ).SingleOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró el pago en efectivo.");
        }

        if (data.Payment.ProveedorPago != CashSource ||
            data.Payment.Estado != "COMPLETED" ||
            data.Order.EstadoPedido != "Entregado" ||
            string.IsNullOrWhiteSpace(data.Order.CodRepartidor))
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "La compensación en efectivo requiere un pedido entregado, cobro confirmado y repartidor asignado.");
        }

        var existingId = await _dbContext.LiquidacionesRepartidor
            .AsNoTracking()
            .Where(item => item.IdPedido == data.Order.IdPedido)
            .Select(item => (long?)item.IdLiquidacion)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
        {
            return await GetInternalByIdAsync(
                existingId.Value,
                cancellationToken);
        }

        var settlement = new LiquidacionRepartidor
        {
            IdPedido = data.Order.IdPedido,
            CodRepartidor = data.Order.CodRepartidor,
            IdPago = data.Payment.IdPago,
            MetodoOrigen = CashSource,
            Estado = CashCollected,
            MontoRepartidorLocal = data.Order.PagoRepartidor,
            MonedaLocal = NormalizeCurrency(_payPalOptions.LocalCurrency),
            MontoEfectivoCobrado = data.Order.TotalCobrado,
            MontoEfectivoARemitir = Math.Max(
                0m,
                data.Order.TotalCobrado - data.Order.PagoRepartidor),
            ClaveIdempotencia = $"DRIVER-CASH-ORDER-{data.Order.IdPedido}",
            CreadoElUtc = DateTime.UtcNow,
            CompletadoElUtc = data.Payment.CompletadoElUtc ?? DateTime.UtcNow,
            Observaciones =
                "El repartidor recibió el efectivo del cliente y conserva el monto de entrega indicado."
        };

        _dbContext.LiquidacionesRepartidor.Add(settlement);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            var concurrentId = await _dbContext.LiquidacionesRepartidor
                .AsNoTracking()
                .Where(item => item.IdPedido == data.Order.IdPedido)
                .Select(item => item.IdLiquidacion)
                .SingleAsync(cancellationToken);
            return await GetInternalByIdAsync(concurrentId, cancellationToken);
        }

        await NotifyDriverAsync(
            settlement,
            "driver_cash_registered",
            "Cobro en efectivo registrado",
            $"Se registró tu pago de entrega por {settlement.MontoRepartidorLocal:0.00} {settlement.MonedaLocal}.",
            cancellationToken);

        return await GetInternalByIdAsync(
            settlement.IdLiquidacion,
            cancellationToken);
    }

    public Task<DriverSettlementResult> RetryPayPalAsync(
        long settlementId,
        CancellationToken cancellationToken = default) =>
        ProcessPayPalAsync(settlementId, cancellationToken);

    public async Task<DriverSettlementResult> RefreshPayPalAsync(
        long settlementId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var settlement = await _dbContext.LiquidacionesRepartidor
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdLiquidacion == settlementId,
                cancellationToken);
        if (settlement is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró la liquidación.");
        }

        if (!CanAccess(
                settlement.CodRepartidor,
                actorType,
                actorReferenceId,
                isAdministrator))
        {
            return Failure(
                DriverSettlementStatus.Forbidden,
                "La cuenta no puede actualizar esta liquidación.");
        }

        return string.IsNullOrWhiteSpace(settlement.LoteProveedorId)
            ? await ProcessPayPalAsync(settlementId, cancellationToken)
            : await RefreshProviderStatusAsync(settlementId, cancellationToken);
    }

    public async Task<DriverSettlementResult> ConfirmCashRemittanceAsync(
        long settlementId,
        string administratorCode,
        ConfirmDriverCashRemittanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settlement = await _dbContext.LiquidacionesRepartidor
            .SingleOrDefaultAsync(
                item => item.IdLiquidacion == settlementId,
                cancellationToken);
        if (settlement is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró la liquidación.");
        }

        if (settlement.MetodoOrigen != CashSource)
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "La conciliación de efectivo solo aplica a liquidaciones CASH.");
        }

        if (settlement.Estado == CashReconciled)
        {
            return await GetInternalByIdAsync(
                settlement.IdLiquidacion,
                cancellationToken);
        }

        if (settlement.Estado != CashCollected)
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "El efectivo no está pendiente de conciliación.");
        }

        settlement.Estado = CashReconciled;
        settlement.EfectivoConciliadoElUtc = DateTime.UtcNow;
        settlement.EfectivoConciliadoPor = NormalizeCode(administratorCode);
        settlement.ReferenciaConciliacion = request.Reference.Trim();
        settlement.Observaciones = CleanOptional(request.Observations);
        settlement.DetalleFallo = null;

        // Si el reembolso incluyó el envío antes de que el repartidor entregara
        // el efectivo, el monto se retuvo dentro de esta misma remesa. Al
        // conciliar la remesa queda recuperado, sin alterar el historial de la
        // liquidación original.
        var adjustments = await _dbContext.AjustesLiquidacionRepartidor
            .Where(item =>
                item.IdLiquidacion == settlement.IdLiquidacion &&
                item.Estado == RefundRecoveryAppliedToCashRemittance)
            .ToArrayAsync(cancellationToken);
        foreach (var adjustment in adjustments)
        {
            adjustment.Estado = RefundRecoveryRecovered;
            adjustment.ConfirmadoElUtc = settlement.EfectivoConciliadoElUtc;
            adjustment.ConfirmadoPor = settlement.EfectivoConciliadoPor;
            adjustment.ReferenciaConfirmacion = settlement.ReferenciaConciliacion;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetInternalByIdAsync(
            settlement.IdLiquidacion,
            cancellationToken);
    }

    public async Task<DriverSettlementResult> EnsureRefundRecoveryAsync(
        long refundId,
        CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.ReembolsosPedidos
            .SingleOrDefaultAsync(
                item => item.IdReembolso == refundId,
                cancellationToken);
        if (refund is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró el reembolso.");
        }

        // Un ajuste solo procede después de que el dinero realmente se devolvió
        // al cliente y únicamente por la parte de envío aprobada.
        if (refund.Estado != "COMPLETED" ||
            refund.MontoEntregaReembolsado <= 0m)
        {
            return new(DriverSettlementStatus.Success);
        }

        var orderId = await
        (
            from salesReturn in _dbContext.Devoluciones.AsNoTracking()
            join invoice in _dbContext.Facturas.AsNoTracking()
                on salesReturn.IdFactura equals invoice.IdFactura
            where salesReturn.IdDevolucion == refund.IdDevolucion
            select (int?)invoice.IdPedido
        ).SingleOrDefaultAsync(cancellationToken);
        if (!orderId.HasValue)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró el pedido asociado al reembolso.");
        }

        var settlement = await _dbContext.LiquidacionesRepartidor
            .SingleOrDefaultAsync(
                item => item.IdPedido == orderId.Value,
                cancellationToken);

        // No todos los pedidos devuelven envío ni todos generan pago al
        // repartidor. En ese caso el reembolso sigue siendo válido, solo no hay
        // nada que recuperar.
        if (settlement is null || settlement.MontoRepartidorLocal <= 0m)
        {
            return new(DriverSettlementStatus.Success);
        }

        var existing = await BuildRefundAdjustmentResponseQuery(
                _dbContext.AjustesLiquidacionRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdReembolso == refundId))
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new(
                DriverSettlementStatus.Success,
                RefundAdjustment: existing);
        }

        var alreadyReversed = await _dbContext.AjustesLiquidacionRepartidor
            .AsNoTracking()
            .Where(item =>
                item.IdLiquidacion == settlement.IdLiquidacion &&
                item.Tipo == RefundRecoveryType)
            .Select(item => (decimal?)item.MontoLocal)
            .SumAsync(cancellationToken) ?? 0m;
        var availableForRecovery = Math.Max(
            0m,
            settlement.MontoRepartidorLocal - alreadyReversed);
        var recoveryAmount = Math.Min(
            refund.MontoEntregaReembolsado,
            availableForRecovery);

        if (recoveryAmount <= 0m)
        {
            return new(DriverSettlementStatus.Success);
        }

        var canRetainInCashRemittance =
            settlement.MetodoOrigen == CashSource &&
            settlement.Estado == CashCollected;
        var canRetainBeforePayPalPayout =
            settlement.MetodoOrigen == PayPalSource &&
            string.IsNullOrWhiteSpace(settlement.LoteProveedorId) &&
            settlement.Estado is PendingConfiguration or Pending;
        var nowUtc = DateTime.UtcNow;
        var adjustment = new AjusteLiquidacionRepartidor
        {
            IdLiquidacion = settlement.IdLiquidacion,
            IdReembolso = refund.IdReembolso,
            Tipo = RefundRecoveryType,
            Estado = canRetainInCashRemittance
                ? RefundRecoveryAppliedToCashRemittance
                : canRetainBeforePayPalPayout
                    ? RefundRecoveryRecovered
                : RefundRecoveryPending,
            MetodoRecuperacion = canRetainInCashRemittance
                ? CashRemittanceRetention
                : canRetainBeforePayPalPayout
                    ? PayPalPayoutRetention
                : FutureRecovery,
            MontoLocal = recoveryAmount,
            MonedaLocal = settlement.MonedaLocal,
            CreadoElUtc = nowUtc,
            ConfirmadoElUtc = canRetainBeforePayPalPayout ? nowUtc : null,
            ConfirmadoPor = canRetainBeforePayPalPayout ? "SYSTEM" : null,
            ReferenciaConfirmacion = canRetainBeforePayPalPayout
                ? "RETENCION-ANTES-PAGO"
                : null,
            Observaciones = canRetainInCashRemittance
                ? "El reembolso incluyó el envío; el monto se añadió a la remesa pendiente del repartidor."
                : canRetainBeforePayPalPayout
                    ? "El reembolso incluyó el envío; el pago PayPal aún no se había enviado y el monto se retuvo automáticamente."
                : "El reembolso incluyó el envío; se requiere recuperar el monto ya acreditado o conciliado al repartidor."
        };

        if (canRetainInCashRemittance)
        {
            settlement.MontoEfectivoARemitir += recoveryAmount;
        }
        else if (canRetainBeforePayPalPayout &&
                 recoveryAmount >= availableForRecovery)
        {
            settlement.Estado = PayPalPayoutHeld;
            settlement.DetalleFallo =
                "El pago de entrega fue retenido porque el reembolso incluyó el costo de envío.";
        }

        _dbContext.AjustesLiquidacionRepartidor.Add(adjustment);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            var concurrent = await BuildRefundAdjustmentResponseQuery(
                    _dbContext.AjustesLiquidacionRepartidor
                        .AsNoTracking()
                        .Where(item => item.IdReembolso == refundId))
                .SingleOrDefaultAsync(cancellationToken);
            return concurrent is null
                ? Failure(
                    DriverSettlementStatus.InvalidState,
                    "No se pudo registrar el ajuste de recuperación del repartidor.")
                : new(
                    DriverSettlementStatus.Success,
                    RefundAdjustment: concurrent);
        }

        var response = await BuildRefundAdjustmentResponseQuery(
                _dbContext.AjustesLiquidacionRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdAjuste == adjustment.IdAjuste))
            .SingleAsync(cancellationToken);
        return new(
            DriverSettlementStatus.Success,
            RefundAdjustment: response);
    }

    public async Task<DriverSettlementResult> GetRefundAdjustmentsAsync(
        long settlementId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var settlement = await BuildResponseQuery(
                _dbContext.LiquidacionesRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdLiquidacion == settlementId))
            .SingleOrDefaultAsync(cancellationToken);
        if (settlement is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró la liquidación.");
        }

        if (!CanAccess(
                settlement.DriverCode,
                actorType,
                actorReferenceId,
                isAdministrator))
        {
            return Failure(
                DriverSettlementStatus.Forbidden,
                "La cuenta no puede consultar estos ajustes.");
        }

        var adjustments = await BuildRefundAdjustmentResponseQuery(
                _dbContext.AjustesLiquidacionRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdLiquidacion == settlementId)
                    .OrderByDescending(item => item.CreadoElUtc)
                    .ThenByDescending(item => item.IdAjuste))
            .ToArrayAsync(cancellationToken);

        return new(
            DriverSettlementStatus.Success,
            RefundAdjustments: new GetDriverSettlementRefundAdjustmentsResponse(
                settlementId,
                adjustments));
    }

    public async Task<DriverSettlementResult> ConfirmRefundRecoveryAsync(
        long adjustmentId,
        string administratorCode,
        ConfirmDriverRefundRecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adjustment = await _dbContext.AjustesLiquidacionRepartidor
            .SingleOrDefaultAsync(
                item => item.IdAjuste == adjustmentId,
                cancellationToken);
        if (adjustment is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró el ajuste de recuperación.");
        }

        if (adjustment.Estado == RefundRecoveryRecovered)
        {
            var completed = await BuildRefundAdjustmentResponseQuery(
                    _dbContext.AjustesLiquidacionRepartidor
                        .AsNoTracking()
                        .Where(item => item.IdAjuste == adjustment.IdAjuste))
                .SingleAsync(cancellationToken);
            return new(
                DriverSettlementStatus.Success,
                RefundAdjustment: completed);
        }

        if (adjustment.Estado != RefundRecoveryPending)
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "Este ajuste se recupera automáticamente al conciliar la remesa de efectivo.");
        }

        adjustment.Estado = RefundRecoveryRecovered;
        adjustment.ConfirmadoElUtc = DateTime.UtcNow;
        adjustment.ConfirmadoPor = NormalizeCode(administratorCode);
        adjustment.ReferenciaConfirmacion = request.Reference.Trim();
        adjustment.Observaciones = CleanOptional(request.Observations) ??
            adjustment.Observaciones;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildRefundAdjustmentResponseQuery(
                _dbContext.AjustesLiquidacionRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdAjuste == adjustment.IdAjuste))
            .SingleAsync(cancellationToken);
        return new(
            DriverSettlementStatus.Success,
            RefundAdjustment: response);
    }

    private async Task<DriverSettlementResult> ProcessPayPalAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var settlement = await _dbContext.LiquidacionesRepartidor
            .SingleOrDefaultAsync(
                item => item.IdLiquidacion == settlementId,
                cancellationToken);
        if (settlement is null)
        {
            return Failure(
                DriverSettlementStatus.NotFound,
                "No se encontró la liquidación.");
        }

        if (settlement.MetodoOrigen != PayPalSource)
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "Solo las liquidaciones PayPal se envían electrónicamente.");
        }

        if (settlement.Estado == Completed)
        {
            return await GetInternalByIdAsync(settlementId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(settlement.LoteProveedorId))
        {
            return await RefreshProviderStatusAsync(
                settlementId,
                cancellationToken);
        }

        var reversedDriverAmount = await _dbContext.AjustesLiquidacionRepartidor
            .AsNoTracking()
            .Where(item => item.IdLiquidacion == settlement.IdLiquidacion)
            .Select(item => (decimal?)item.MontoLocal)
            .SumAsync(cancellationToken) ?? 0m;
        var payableDriverAmount = Math.Max(
            0m,
            settlement.MontoRepartidorLocal - reversedDriverAmount);
        if (payableDriverAmount <= 0m)
        {
            if (settlement.Estado != PayPalPayoutHeld)
            {
                settlement.Estado = PayPalPayoutHeld;
                settlement.DetalleFallo =
                    "El pago de entrega fue retenido porque el reembolso incluyó el costo de envío.";
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return await GetInternalByIdAsync(settlementId, cancellationToken);
        }

        var account = await _dbContext.CuentasPagoRepartidor
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.CodRepartidor == settlement.CodRepartidor &&
                    item.Activa,
                cancellationToken);
        var conversion = CalculateProviderAmount(payableDriverAmount);
        var configurationDetail = GetPayPalConfigurationDetail(account, conversion);
        if (configurationDetail is not null)
        {
            settlement.Estado = PendingConfiguration;
            settlement.CorreoPaypalDestino = account?.CorreoPaypal;
            settlement.DetalleFallo = configurationDetail;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return await GetInternalByIdAsync(
                settlementId,
                cancellationToken,
                DriverSettlementStatus.ConfigurationError);
        }

        settlement.Estado = Processing;
        settlement.CorreoPaypalDestino = account!.CorreoPaypal;
        settlement.MontoProveedor = conversion.Amount;
        settlement.MonedaProveedor = NormalizeCurrency(_payPalOptions.PayPalCurrency);
        settlement.TipoCambio = conversion.ExchangeRate;
        settlement.Intentos++;
        settlement.UltimoIntentoUtc = DateTime.UtcNow;
        settlement.DetalleFallo = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var orderCode = await _dbContext.Pedidos
            .AsNoTracking()
            .Where(item => item.IdPedido == settlement.IdPedido)
            .Select(item => item.CodPedido)
            .SingleAsync(cancellationToken);

        PayPalPayoutGatewayResult providerResult;
        try
        {
            providerResult = await _payPalGateway.CreatePayoutAsync(
                conversion.Amount!.Value,
                NormalizeCurrency(_payPalOptions.PayPalCurrency),
                account.CorreoPaypal,
                settlement.ClaveIdempotencia,
                $"{settlement.ClaveIdempotencia}-ITEM",
                _options.EmailSubject,
                _options.EmailMessage,
                $"Pago de entrega del pedido {orderCode}.",
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "No se pudo contactar PayPal para la liquidación {SettlementId}.",
                settlement.IdLiquidacion);
            providerResult = new(
                false,
                null,
                null,
                null,
                null,
                null,
                "No fue posible comunicarse con PayPal Payouts.");
        }

        if (!providerResult.Success ||
            string.IsNullOrWhiteSpace(providerResult.BatchId))
        {
            settlement.Estado = Error;
            settlement.DetalleFallo = LimitDetail(
                providerResult.Error ??
                "PayPal no confirmó la creación de la liquidación.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return await GetInternalByIdAsync(
                settlementId,
                cancellationToken,
                DriverSettlementStatus.ProviderError);
        }

        ApplyProviderResult(settlement, providerResult);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await RefreshProviderStatusAsync(
            settlementId,
            cancellationToken);
    }

    private async Task<DriverSettlementResult> RefreshProviderStatusAsync(
        long settlementId,
        CancellationToken cancellationToken)
    {
        var settlement = await _dbContext.LiquidacionesRepartidor
            .SingleAsync(
                item => item.IdLiquidacion == settlementId,
                cancellationToken);
        if (settlement.MetodoOrigen != PayPalSource ||
            string.IsNullOrWhiteSpace(settlement.LoteProveedorId))
        {
            return Failure(
                DriverSettlementStatus.InvalidState,
                "La liquidación todavía no tiene un lote PayPal para consultar.");
        }

        settlement.UltimoIntentoUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        PayPalPayoutGatewayResult providerResult;
        try
        {
            providerResult = await _payPalGateway.GetPayoutAsync(
                settlement.LoteProveedorId,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "No se pudo actualizar la liquidación PayPal {SettlementId}.",
                settlement.IdLiquidacion);
            return await GetInternalByIdAsync(
                settlementId,
                cancellationToken,
                DriverSettlementStatus.ProviderError,
                "No fue posible consultar el estado de la liquidación en PayPal.");
        }

        if (!providerResult.Success)
        {
            return await GetInternalByIdAsync(
                settlementId,
                cancellationToken,
                DriverSettlementStatus.ProviderError,
                providerResult.Error ??
                "PayPal no devolvió el estado de la liquidación.");
        }

        var wasCompleted = settlement.Estado == Completed;
        ApplyProviderResult(settlement, providerResult);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!wasCompleted && settlement.Estado == Completed)
        {
            await NotifyDriverAsync(
                settlement,
                "driver_payout_completed",
                "Pago de entrega acreditado",
                $"Se acreditó tu pago de {settlement.MontoRepartidorLocal:0.00} {settlement.MonedaLocal}.",
                cancellationToken);
        }

        return await GetInternalByIdAsync(
            settlementId,
            cancellationToken,
            settlement.Estado == Error
                ? DriverSettlementStatus.ProviderError
                : null);
    }

    private void ApplyProviderResult(
        LiquidacionRepartidor settlement,
        PayPalPayoutGatewayResult result)
    {
        settlement.LoteProveedorId = result.BatchId ?? settlement.LoteProveedorId;
        settlement.ItemProveedorId = result.ItemId ?? settlement.ItemProveedorId;
        settlement.TransaccionProveedorId =
            result.TransactionId ?? settlement.TransaccionProveedorId;

        var itemStatus = result.TransactionStatus?.Trim().ToUpperInvariant();
        var batchStatus = result.BatchStatus?.Trim().ToUpperInvariant();

        if (itemStatus == "SUCCESS")
        {
            settlement.Estado = Completed;
            settlement.CompletadoElUtc ??= DateTime.UtcNow;
            settlement.DetalleFallo = null;
            return;
        }

        if (itemStatus is "FAILED" or "RETURNED" or "REFUNDED" or "REVERSED" ||
            batchStatus is "DENIED" or "CANCELED")
        {
            settlement.Estado = Error;
            settlement.DetalleFallo = LimitDetail(
                result.Error ??
                $"PayPal informó el estado {itemStatus ?? batchStatus ?? "DESCONOCIDO"}.");
            return;
        }

        settlement.Estado = Processing;
        settlement.DetalleFallo = null;
    }

    private string? GetPayPalConfigurationDetail(
        CuentaPagoRepartidor? account,
        ProviderConversion conversion)
    {
        if (!_options.Enabled)
        {
            return "Las liquidaciones automáticas están deshabilitadas en la configuración.";
        }

        if (account is null)
        {
            return "El repartidor debe configurar su correo PayPal antes de recibir el pago.";
        }

        if (conversion.Error is not null)
        {
            return conversion.Error;
        }

        return null;
    }

    private ProviderConversion CalculateProviderAmount(decimal localAmount)
    {
        if (_payPalOptions.LocalCurrencyUnitsPerPayPalUnit <= 0m)
        {
            return new(
                null,
                null,
                "PayPal:LocalCurrencyUnitsPerPayPalUnit debe ser mayor que cero.");
        }

        var amount = decimal.Round(
            localAmount / _payPalOptions.LocalCurrencyUnitsPerPayPalUnit,
            2,
            MidpointRounding.AwayFromZero);
        if (amount < 0.01m)
        {
            return new(
                null,
                _payPalOptions.LocalCurrencyUnitsPerPayPalUnit,
                "El pago convertido es menor que el mínimo admitido por PayPal.");
        }

        return new(
            amount,
            _payPalOptions.LocalCurrencyUnitsPerPayPalUnit,
            null);
    }

    private async Task<DriverSettlementResult> GetInternalByIdAsync(
        long settlementId,
        CancellationToken cancellationToken,
        DriverSettlementStatus? overrideStatus = null,
        string? overrideDetail = null)
    {
        var response = await BuildResponseQuery(
                _dbContext.LiquidacionesRepartidor
                    .AsNoTracking()
                    .Where(item => item.IdLiquidacion == settlementId))
            .SingleAsync(cancellationToken);
        return new(
            overrideStatus ?? DriverSettlementStatus.Success,
            Settlement: response,
            Detail: overrideDetail ?? response.FailureDetail);
    }

    private IQueryable<DriverSettlementResponse> BuildResponseQuery(
        IQueryable<LiquidacionRepartidor> settlements) =>
        from settlement in settlements
        join order in _dbContext.Pedidos.AsNoTracking()
            on settlement.IdPedido equals order.IdPedido
        join driver in _dbContext.Repartidores.AsNoTracking()
            on settlement.CodRepartidor equals driver.CodRepartidor
        let reversedDriverAmount =
            _dbContext.AjustesLiquidacionRepartidor
                .AsNoTracking()
                .Where(item => item.IdLiquidacion == settlement.IdLiquidacion)
                .Select(item => (decimal?)item.MontoLocal)
                .Sum() ?? 0m
        let recoveryStatus =
            _dbContext.AjustesLiquidacionRepartidor
                .AsNoTracking()
                .Where(item => item.IdLiquidacion == settlement.IdLiquidacion)
                .OrderByDescending(item => item.CreadoElUtc)
                .ThenByDescending(item => item.IdAjuste)
                .Select(item => item.Estado)
                .FirstOrDefault()
        select new DriverSettlementResponse(
            settlement.IdLiquidacion,
            settlement.IdPedido,
            order.CodPedido,
            settlement.CodRepartidor,
            driver.NombreRepartidor + " " + driver.ApellidosRepartidor,
            settlement.IdPago,
            settlement.MetodoOrigen,
            settlement.Estado,
            settlement.MontoRepartidorLocal,
            settlement.MonedaLocal,
            settlement.MontoProveedor,
            settlement.MonedaProveedor,
            settlement.TipoCambio,
            settlement.MontoEfectivoCobrado,
            settlement.MontoEfectivoARemitir,
            settlement.CorreoPaypalDestino,
            settlement.LoteProveedorId,
            settlement.ItemProveedorId,
            settlement.TransaccionProveedorId,
            settlement.ClaveIdempotencia,
            settlement.Intentos,
            settlement.CreadoElUtc,
            settlement.UltimoIntentoUtc,
            settlement.CompletadoElUtc,
            settlement.EfectivoConciliadoElUtc,
            settlement.EfectivoConciliadoPor,
            settlement.ReferenciaConciliacion,
            settlement.Observaciones,
            settlement.DetalleFallo,
            reversedDriverAmount,
            settlement.MontoRepartidorLocal > reversedDriverAmount
                ? settlement.MontoRepartidorLocal - reversedDriverAmount
                : 0m,
            recoveryStatus ?? "SIN_AJUSTE");

    private IQueryable<DriverSettlementRefundAdjustmentResponse>
        BuildRefundAdjustmentResponseQuery(
            IQueryable<AjusteLiquidacionRepartidor> adjustments) =>
        from adjustment in adjustments
        join settlement in _dbContext.LiquidacionesRepartidor.AsNoTracking()
            on adjustment.IdLiquidacion equals settlement.IdLiquidacion
        join order in _dbContext.Pedidos.AsNoTracking()
            on settlement.IdPedido equals order.IdPedido
        join driver in _dbContext.Repartidores.AsNoTracking()
            on settlement.CodRepartidor equals driver.CodRepartidor
        select new DriverSettlementRefundAdjustmentResponse(
            adjustment.IdAjuste,
            adjustment.IdLiquidacion,
            settlement.IdPedido,
            order.CodPedido,
            settlement.CodRepartidor,
            driver.NombreRepartidor + " " + driver.ApellidosRepartidor,
            adjustment.IdReembolso,
            adjustment.Tipo,
            adjustment.Estado,
            adjustment.MetodoRecuperacion,
            adjustment.MontoLocal,
            adjustment.MonedaLocal,
            adjustment.CreadoElUtc,
            adjustment.ConfirmadoElUtc,
            adjustment.ConfirmadoPor,
            adjustment.ReferenciaConfirmacion,
            adjustment.Observaciones);

    private async Task NotifyDriverAsync(
        LiquidacionRepartidor settlement,
        string type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var accountId = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .Where(item =>
                item.Estado &&
                item.TipoUsuario == "Repartidor" &&
                item.ReferenciaId == settlement.CodRepartidor)
            .Select(item => (int?)item.IdCuenta)
            .SingleOrDefaultAsync(cancellationToken);
        if (!accountId.HasValue)
        {
            return;
        }

        await _pushSender.SendToAccountAsync(
            accountId.Value,
            type,
            title,
            body,
            new Dictionary<string, string>
            {
                ["type"] = type,
                ["settlementId"] = settlement.IdLiquidacion.ToString(CultureInfo.InvariantCulture),
                ["orderId"] = settlement.IdPedido.ToString(CultureInfo.InvariantCulture),
                ["status"] = settlement.Estado
            },
            cancellationToken);
    }

    private static bool CanAccess(
        string driverCode,
        string actorType,
        string actorReferenceId,
        bool isAdministrator) =>
        isAdministrator ||
        (actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
         driverCode.Equals(
             NormalizeCode(actorReferenceId),
             StringComparison.OrdinalIgnoreCase));

    private static DriverPayoutAccountResponse MapAccount(
        CuentaPagoRepartidor account) =>
        new(
            account.CodRepartidor,
            account.Proveedor,
            account.CorreoPaypal,
            account.Activa,
            account.CreadoElUtc,
            account.ActualizadoElUtc);

    private static DriverSettlementResult Failure(
        DriverSettlementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string LimitDetail(string value) =>
        value.Length <= 500 ? value : value[..500];

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private sealed record ProviderConversion(
        decimal? Amount,
        decimal? ExchangeRate,
        string? Error);
}
