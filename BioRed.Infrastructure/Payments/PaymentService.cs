using System.Data;
using BioRed.Application.Payments;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BioRed.Infrastructure.Payments;

public sealed class PaymentService : IPaymentService
{
    private const int PaidReceivableStatusId = 31;
    private const int PartialReceivableStatusId = 32;
    private const string PayPalPaymentCode = "PAGO-PAYPAL";
    private const string CashPaymentCode = "PAGO-EFECTIVO";
    private const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    private readonly BioRedDbContext _dbContext;
    private readonly IPayPalGateway _payPalGateway;
    private readonly IPaymentReceiptService _receiptService;
    private readonly PayPalOptions _options;

    public PaymentService(
        BioRedDbContext dbContext,
        IPayPalGateway payPalGateway,
        IPaymentReceiptService receiptService,
        IOptions<PayPalOptions> options)
    {
        _dbContext = dbContext;
        _payPalGateway = payPalGateway;
        _receiptService = receiptService;
        _options = options.Value;
    }

    public async Task<PaymentManagementResult> GetByOrderAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Pedidos.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdPedido == orderId,
                cancellationToken);

        if (order is null)
        {
            return NotFound(orderId);
        }

        if (!CanAccessOrder(
                order,
                actorType,
                actorReferenceId,
                isAdministrator))
        {
            return Failure(
                PaymentManagementStatus.Forbidden,
                "La cuenta no puede consultar los pagos de este pedido.");
        }

        var payments = await _dbContext.PagosPedidos.AsNoTracking()
            .Where(item => item.IdPedido == orderId)
            .OrderBy(item => item.CreadoElUtc)
            .ThenBy(item => item.IdPago)
            .ToArrayAsync(cancellationToken);
        var responses = payments
            .Select(item => MapPayment(item, order.CodPedido))
            .ToArray();

        return new(
            PaymentManagementStatus.Success,
            OrderPayments: new GetOrderPaymentsResponse(
                order.IdPedido,
                order.CodPedido,
                order.TotalCobrado,
                order.CodTipoPago,
                payments.Any(item => item.Estado == "COMPLETED"),
                responses));
    }

    public async Task<PaymentManagementResult> CreatePayPalOrderAsync(
        int orderId,
        string clientCode,
        int actorAccountId,
        CreatePayPalOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedClient = NormalizeCode(clientCode);
        var configurationError = ValidatePayPalConfiguration();
        if (configurationError is not null)
        {
            return Failure(PaymentManagementStatus.ConfigurationError, configurationError);
        }

        var order = await _dbContext.Pedidos.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.IdPedido == orderId &&
                        item.CodCliente == normalizedClient,
                cancellationToken);

        if (order is null)
        {
            return NotFound(orderId);
        }

        if (order.CodTipoPago != PayPalPaymentCode)
        {
            return Failure(
                PaymentManagementStatus.InvalidPaymentType,
                "El pedido no fue creado con el tipo de pago PAGO-PAYPAL.");
        }

        if (order.EstadoPedido.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                PaymentManagementStatus.InvalidOrderState,
                "No se puede pagar un pedido cancelado.");
        }

        var existing = await _dbContext.PagosPedidos.AsNoTracking()
            .Where(item =>
                item.IdPedido == orderId &&
                item.ProveedorPago == "PAYPAL" &&
                item.Estado != "FAILED")
            .OrderByDescending(item => item.IdPago)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return new(
                PaymentManagementStatus.Success,
                MapPayment(existing, order.CodPedido));
        }

        if (!IsValidLocalAmount(order.TotalCobrado))
        {
            return Failure(
                PaymentManagementStatus.InvalidAmount,
                "El total del pedido no es válido para procesar el pago.");
        }

        var providerAmount = ToProviderAmount(order.TotalCobrado);
        if (providerAmount < 0.01m)
        {
            return Failure(
                PaymentManagementStatus.InvalidAmount,
                "El total convertido es menor que el mínimo admitido por PayPal.");
        }

        var idempotencyKey = $"BIORED-ORDER-{order.IdPedido}";
        PayPalOrderGatewayResult providerResult;
        try
        {
            providerResult = await _payPalGateway.CreateOrderAsync(
                providerAmount,
                NormalizeCurrency(_options.PayPalCurrency),
                idempotencyKey,
                $"Pedido {order.CodPedido}",
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            return Failure(
                PaymentManagementStatus.ProviderError,
                "No fue posible comunicarse con PayPal Sandbox.");
        }

        if (!providerResult.Success ||
            providerResult.OrderId is null ||
            providerResult.Status is not ("CREATED" or "APPROVED") ||
            providerResult.Amount != providerAmount ||
            !CurrencyEquals(providerResult.Currency, _options.PayPalCurrency))
        {
            return Failure(
                PaymentManagementStatus.ProviderError,
                providerResult.Error ??
                "PayPal no confirmó el identificador, monto o moneda de la orden.");
        }

        var payment = new PagoPedido
        {
            IdPedido = order.IdPedido,
            CodCliente = order.CodCliente,
            CodTipoPago = PayPalPaymentCode,
            ProveedorPago = "PAYPAL",
            Estado = providerResult.Status,
            MontoLocal = order.TotalCobrado,
            MonedaLocal = NormalizeCurrency(_options.LocalCurrency),
            MontoProveedor = providerAmount,
            MonedaProveedor = NormalizeCurrency(_options.PayPalCurrency),
            TipoCambio = _options.LocalCurrencyUnitsPerPayPalUnit,
            OrdenProveedorId = providerResult.OrderId,
            UrlAprobacion = providerResult.ApprovalUrl,
            ClaveIdempotencia = idempotencyKey,
            CreadoPorCuentaId = actorAccountId,
            CreadoElUtc = DateTime.UtcNow,
            ReferenciaExterna = CleanOptional(request.RequestReference)
        };

        _dbContext.PagosPedidos.Add(payment);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            var concurrent = await _dbContext.PagosPedidos.AsNoTracking()
                .Where(item => item.IdPedido == orderId && item.Estado != "FAILED")
                .OrderByDescending(item => item.IdPago)
                .FirstOrDefaultAsync(cancellationToken);

            return concurrent is null
                ? Failure(
                    PaymentManagementStatus.ProviderError,
                    "La orden PayPal fue creada, pero no pudo conciliarse localmente.")
                : new(
                    PaymentManagementStatus.Success,
                    MapPayment(concurrent, order.CodPedido));
        }

        return new(
            PaymentManagementStatus.Success,
            MapPayment(payment, order.CodPedido));
    }

    public async Task<PaymentManagementResult> CapturePayPalOrderAsync(
        string providerOrderId,
        string clientCode,
        CancellationToken cancellationToken = default)
    {
        var configurationError = ValidatePayPalConfiguration();
        if (configurationError is not null)
        {
            return Failure(PaymentManagementStatus.ConfigurationError, configurationError);
        }

        var normalizedOrderId = providerOrderId.Trim();
        var normalizedClient = NormalizeCode(clientCode);
        var payment = await _dbContext.PagosPedidos.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OrdenProveedorId == normalizedOrderId,
                cancellationToken);

        if (payment is null)
        {
            return Failure(
                PaymentManagementStatus.NotFound,
                "No existe una orden PayPal local con ese identificador.");
        }

        var order = await _dbContext.Pedidos.AsNoTracking()
            .SingleAsync(
                item => item.IdPedido == payment.IdPedido,
                cancellationToken);

        if (payment.CodCliente != normalizedClient ||
            order.CodCliente != normalizedClient)
        {
            return Failure(
                PaymentManagementStatus.Forbidden,
                "La orden PayPal pertenece a otro cliente.");
        }

        if (order.EstadoPedido.Equals(
                "Cancelado",
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                PaymentManagementStatus.InvalidOrderState,
                "No se puede capturar el pago de un pedido cancelado.");
        }

        if (payment.Estado == "COMPLETED")
        {
            await _receiptService.EnsureForPaymentAsync(
                payment.IdPago,
                cancellationToken);
            return new(
                PaymentManagementStatus.Success,
                MapPayment(payment, order.CodPedido));
        }

        if (payment.Estado is not ("CREATED" or "APPROVED"))
        {
            return Failure(
                PaymentManagementStatus.InvalidOrderState,
                $"La transacción se encuentra en estado {payment.Estado}.");
        }

        var receivableBalance = await GetReceivableBalanceAsync(
            order.IdPedido,
            cancellationToken);
        if (receivableBalance.HasValue &&
            receivableBalance.Value != payment.MontoLocal)
        {
            return Failure(
                PaymentManagementStatus.InvalidAmount,
                "El saldo contable cambió y ya no coincide con el pago que se desea capturar.");
        }

        PayPalOrderGatewayResult providerResult;
        try
        {
            providerResult = await _payPalGateway.CaptureOrderAsync(
                normalizedOrderId,
                $"CAPTURE-{payment.IdPago}",
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            return Failure(
                PaymentManagementStatus.ProviderError,
                "No fue posible comunicarse con PayPal Sandbox.");
        }

        if (!providerResult.Success ||
            providerResult.Status != "COMPLETED" ||
            providerResult.CaptureId is null ||
            providerResult.Amount != payment.MontoProveedor ||
            !CurrencyEquals(providerResult.Currency, payment.MonedaProveedor))
        {
            return Failure(
                PaymentManagementStatus.ProviderError,
                providerResult.Error ??
                "PayPal no confirmó una captura completada con el monto y moneda esperados.");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var trackedPayment = await _dbContext.PagosPedidos.SingleAsync(
                item => item.IdPago == payment.IdPago,
                cancellationToken);
            var trackedOrder = await _dbContext.Pedidos.SingleAsync(
                item => item.IdPedido == payment.IdPedido,
                cancellationToken);

            if (trackedPayment.Estado == "COMPLETED")
            {
                await _receiptService.EnsureForPaymentAsync(
                    trackedPayment.IdPago,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new PaymentManagementResult(
                    PaymentManagementStatus.Success,
                    MapPayment(trackedPayment, trackedOrder.CodPedido));
            }

            if (await _dbContext.PagosPedidos.AsNoTracking().AnyAsync(
                    item => item.IdPedido == trackedOrder.IdPedido &&
                            item.IdPago != trackedPayment.IdPago &&
                            item.Estado == "COMPLETED",
                    cancellationToken))
            {
                return Failure(
                    PaymentManagementStatus.AlreadyPaid,
                    "El pedido ya tiene otro pago completado.");
            }

            trackedPayment.Estado = "COMPLETED";
            trackedPayment.CapturaProveedorId = providerResult.CaptureId;
            trackedPayment.CompletadoElUtc = DateTime.UtcNow;
            trackedPayment.DetalleFallo = null;
            trackedOrder.TransaccionPaypalId = providerResult.CaptureId;

            var applied = await ApplyToReceivableAsync(
                trackedPayment,
                trackedOrder,
                providerResult.CaptureId,
                cancellationToken);
            if (!applied)
            {
                return Failure(
                    PaymentManagementStatus.InvalidAmount,
                    "La captura fue confirmada por PayPal, pero el saldo contable no coincide; requiere conciliación administrativa.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _receiptService.EnsureForPaymentAsync(
                trackedPayment.IdPago,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PaymentManagementResult(
                PaymentManagementStatus.Success,
                MapPayment(trackedPayment, trackedOrder.CodPedido));
        });
    }

    public async Task<PaymentManagementResult> ConfirmCashAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        int actorAccountId,
        bool isAdministrator,
        ConfirmCashPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var order = await _dbContext.Pedidos.SingleOrDefaultAsync(
                item => item.IdPedido == orderId,
                cancellationToken);
            if (order is null)
            {
                return NotFound(orderId);
            }

            var isAssignedDriver =
                actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(order.CodRepartidor) &&
                order.CodRepartidor.Equals(
                    actorReferenceId,
                    StringComparison.OrdinalIgnoreCase);
            if (!isAdministrator && !isAssignedDriver)
            {
                return Failure(
                    PaymentManagementStatus.Forbidden,
                    "Solo el administrador o el repartidor asignado pueden confirmar el efectivo.");
            }

            if (order.CodTipoPago != CashPaymentCode)
            {
                return Failure(
                    PaymentManagementStatus.InvalidPaymentType,
                    "El pedido no fue creado como efectivo contra entrega.");
            }

            if (order.EstadoPedido != "Entregado")
            {
                return Failure(
                    PaymentManagementStatus.InvalidOrderState,
                    "El efectivo solamente puede confirmarse después de entregar el pedido.");
            }

            if (await _dbContext.PagosPedidos.AsNoTracking().AnyAsync(
                    item => item.IdPedido == orderId && item.Estado == "COMPLETED",
                    cancellationToken))
            {
                return Failure(
                    PaymentManagementStatus.AlreadyPaid,
                    "El pedido ya tiene un pago completado.");
            }

            var receivableBalance = await GetReceivableBalanceAsync(
                orderId,
                cancellationToken);
            if (receivableBalance.HasValue &&
                receivableBalance.Value != order.TotalCobrado)
            {
                return Failure(
                    PaymentManagementStatus.InvalidAmount,
                    "El saldo contable ya fue modificado y no coincide con el total del pedido.");
            }

            var nowUtc = DateTime.UtcNow;
            var externalReference = CleanOptional(request.ExternalReference) ??
                $"CASH-{order.IdPedido}-{nowUtc:yyyyMMddHHmmss}";
            var payment = new PagoPedido
            {
                IdPedido = order.IdPedido,
                CodCliente = order.CodCliente,
                CodTipoPago = CashPaymentCode,
                ProveedorPago = "CASH",
                Estado = "COMPLETED",
                MontoLocal = order.TotalCobrado,
                MonedaLocal = NormalizeCurrency(_options.LocalCurrency),
                MontoProveedor = order.TotalCobrado,
                MonedaProveedor = NormalizeCurrency(_options.LocalCurrency),
                TipoCambio = 1m,
                ClaveIdempotencia = $"CASH-ORDER-{order.IdPedido}",
                CreadoPorCuentaId = actorAccountId,
                CreadoElUtc = nowUtc,
                CompletadoElUtc = nowUtc,
                ReferenciaExterna = externalReference,
                Observaciones = CleanOptional(request.Observations)
            };

            _dbContext.PagosPedidos.Add(payment);
            var applied = await ApplyToReceivableAsync(
                payment,
                order,
                externalReference,
                cancellationToken);
            if (!applied)
            {
                return Failure(
                    PaymentManagementStatus.InvalidAmount,
                    "No fue posible conciliar el efectivo con la cuenta por cobrar.");
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _receiptService.EnsureForPaymentAsync(
                payment.IdPago,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PaymentManagementResult(
                PaymentManagementStatus.Success,
                MapPayment(payment, order.CodPedido));
        });
    }

    public async Task<PaymentManagementResult> ProcessRefundAsync(
        string actorReferenceId,
        ProcessRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        if (!await _dbContext.Usuarios.AsNoTracking().AnyAsync(
                item => item.CodUsuario == actorCode && item.Estado,
                cancellationToken))
        {
            return Failure(
                PaymentManagementStatus.NotFound,
                "La cuenta no está vinculada con un usuario administrativo activo.");
        }

        var header = await
        (
            from salesReturn in _dbContext.Devoluciones
            join invoice in _dbContext.Facturas
                on salesReturn.IdFactura equals invoice.IdFactura
            join order in _dbContext.Pedidos
                on invoice.IdPedido equals order.IdPedido
            where salesReturn.IdDevolucion == request.ReturnId
            select new
            {
                Return = salesReturn,
                Invoice = invoice,
                Order = order
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return Failure(
                PaymentManagementStatus.NotFound,
                $"No existe la devolución {request.ReturnId}.");
        }

        var existing = await _dbContext.ReembolsosPedidos.SingleOrDefaultAsync(
            item => item.IdDevolucion == request.ReturnId,
            cancellationToken);
        if (existing is not null && existing.Estado == "COMPLETED")
        {
            return new(
                PaymentManagementStatus.Success,
                Refund: await MapRefundAsync(existing, cancellationToken));
        }

        if (header.Return.ReembolsoPendiente <= 0m)
        {
            return Failure(
                PaymentManagementStatus.RefundNotRequired,
                "La devolución no tiene un reembolso pendiente.");
        }

        var payment = await _dbContext.PagosPedidos
            .Where(item =>
                item.IdPedido == header.Order.IdPedido &&
                item.Estado == "COMPLETED")
            .OrderByDescending(item => item.IdPago)
            .FirstOrDefaultAsync(cancellationToken);

        var provider = payment?.ProveedorPago;
        if (provider is null)
        {
            var paymentType = await
            (
                from movement in _dbContext.CuentasPorCobrarDetalle.AsNoTracking()
                join receivable in _dbContext.CuentasPorCobrar.AsNoTracking()
                    on movement.IdCxc equals receivable.IdCxc
                where receivable.IdFactura == header.Invoice.IdFactura &&
                      movement.TipoMovimiento == "Pago"
                orderby movement.FechaPago descending, movement.IdCxcDetalle descending
                select movement.CodTipoPago
            ).FirstOrDefaultAsync(cancellationToken);

            provider = paymentType == CashPaymentCode ? "CASH" : null;
        }

        if (provider is null)
        {
            return Failure(
                PaymentManagementStatus.ProviderError,
                "No existe un pago conciliado que permita procesar el reembolso.");
        }

        if (provider == "PAYPAL")
        {
            var configurationError = ValidatePayPalConfiguration();
            if (configurationError is not null)
            {
                return Failure(
                    PaymentManagementStatus.ConfigurationError,
                    configurationError);
            }
        }

        var localAmount = header.Return.ReembolsoPendiente;
        var localCurrency = payment?.MonedaLocal ?? NormalizeCurrency(_options.LocalCurrency);
        var providerCurrency = payment?.MonedaProveedor ?? localCurrency;
        var exchangeRate = payment?.TipoCambio ?? 1m;
        var providerAmount = provider == "PAYPAL"
            ? decimal.Round(localAmount / exchangeRate, 2, MidpointRounding.AwayFromZero)
            : localAmount;

        if (payment is not null)
        {
            var previousRefunds = await _dbContext.ReembolsosPedidos
                .AsNoTracking()
                .Where(item =>
                    item.IdPago == payment.IdPago &&
                    item.IdDevolucion != request.ReturnId &&
                    item.Estado != "FAILED")
                .ToArrayAsync(cancellationToken);
            var previousLocalAmount = previousRefunds.Sum(item => item.MontoLocal);
            var previousProviderAmount = previousRefunds.Sum(item => item.MontoProveedor);

            if (previousLocalAmount + localAmount > payment.MontoLocal)
            {
                return Failure(
                    PaymentManagementStatus.InvalidAmount,
                    "Los reembolsos acumulados superarían el pago original.");
            }

            if (provider == "PAYPAL")
            {
                var cumulativeProviderAmount = decimal.Round(
                    (previousLocalAmount + localAmount) / exchangeRate,
                    2,
                    MidpointRounding.AwayFromZero);
                providerAmount = cumulativeProviderAmount - previousProviderAmount;

                if (cumulativeProviderAmount > payment.MontoProveedor)
                {
                    return Failure(
                        PaymentManagementStatus.InvalidAmount,
                        "Los reembolsos convertidos superarían la captura original de PayPal.");
                }
            }
        }

        if (providerAmount < 0.01m)
        {
            return Failure(
                PaymentManagementStatus.InvalidAmount,
                "El monto convertido del reembolso es menor que el mínimo permitido.");
        }

        if (provider == "CASH" &&
            string.IsNullOrWhiteSpace(request.ExternalReference))
        {
            return Failure(
                PaymentManagementStatus.CashReferenceRequired,
                "Indique el número de recibo o referencia de la devolución en efectivo.");
        }

        var refund = existing ?? new ReembolsoPedido
        {
            IdDevolucion = request.ReturnId,
            IdPago = payment?.IdPago,
            ProveedorPago = provider,
            MontoLocal = localAmount,
            MonedaLocal = localCurrency,
            MontoProveedor = providerAmount,
            MonedaProveedor = providerCurrency,
            TipoCambio = exchangeRate,
            ClaveIdempotencia = $"REFUND-RETURN-{request.ReturnId}",
            ProcesadoPor = actorCode,
            CreadoElUtc = DateTime.UtcNow
        };

        refund.IdPago = payment?.IdPago;
        refund.ProveedorPago = provider;
        refund.MontoLocal = localAmount;
        refund.MonedaLocal = localCurrency;
        refund.MontoProveedor = providerAmount;
        refund.MonedaProveedor = providerCurrency;
        refund.TipoCambio = exchangeRate;
        refund.Estado = "PENDING";
        refund.ReferenciaExterna = CleanOptional(request.ExternalReference);
        refund.Observaciones = CleanOptional(request.Observations);
        refund.DetalleFallo = null;

        if (existing is null)
        {
            _dbContext.ReembolsosPedidos.Add(refund);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            var concurrentRefund = await _dbContext.ReembolsosPedidos
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.IdDevolucion == request.ReturnId,
                    cancellationToken);

            if (concurrentRefund is not null &&
                concurrentRefund.Estado == "COMPLETED")
            {
                return new(
                    PaymentManagementStatus.Success,
                    Refund: await MapRefundAsync(
                        concurrentRefund,
                        cancellationToken));
            }

            return Failure(
                PaymentManagementStatus.RefundAlreadyProcessed,
                "El reembolso ya está siendo procesado con la misma devolución.");
        }

        if (provider == "CASH")
        {
            refund.Estado = "COMPLETED";
            refund.CompletadoElUtc = DateTime.UtcNow;
            header.Return.CreditoAplicado += header.Return.ReembolsoPendiente;
            header.Return.ReembolsoPendiente = 0m;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new(
                PaymentManagementStatus.Success,
                Refund: await MapRefundAsync(refund, cancellationToken));
        }

        if (payment?.CapturaProveedorId is null)
        {
            refund.Estado = "FAILED";
            refund.DetalleFallo = "El pago PayPal no conserva un identificador de captura.";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Failure(PaymentManagementStatus.ProviderError, refund.DetalleFallo);
        }

        PayPalRefundGatewayResult providerResult;
        try
        {
            providerResult = await _payPalGateway.RefundCaptureAsync(
                payment.CapturaProveedorId,
                providerAmount,
                providerCurrency,
                refund.ClaveIdempotencia,
                request.Observations,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException)
        {
            refund.Estado = "FAILED";
            refund.DetalleFallo = "No fue posible comunicarse con PayPal Sandbox.";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Failure(PaymentManagementStatus.ProviderError, refund.DetalleFallo);
        }

        if (!providerResult.Success ||
            providerResult.RefundId is null ||
            providerResult.Amount != providerAmount ||
            !CurrencyEquals(providerResult.Currency, providerCurrency))
        {
            refund.Estado = "FAILED";
            refund.DetalleFallo = providerResult.Error ??
                "PayPal no confirmó el identificador, monto o moneda del reembolso.";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Failure(PaymentManagementStatus.ProviderError, refund.DetalleFallo);
        }

        refund.ReembolsoProveedorId = providerResult.RefundId;
        refund.Estado = providerResult.Status == "COMPLETED"
            ? "COMPLETED"
            : "PENDING";
        if (refund.Estado == "COMPLETED")
        {
            refund.CompletadoElUtc = DateTime.UtcNow;
            header.Return.CreditoAplicado += header.Return.ReembolsoPendiente;
            header.Return.ReembolsoPendiente = 0m;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new(
            PaymentManagementStatus.Success,
            Refund: await MapRefundAsync(refund, cancellationToken));
    }

    private async Task<bool> ApplyToReceivableAsync(
        PagoPedido payment,
        Pedido order,
        string externalReference,
        CancellationToken cancellationToken)
    {
        var accounting = await
        (
            from invoice in _dbContext.Facturas
            join receivable in _dbContext.CuentasPorCobrar
                on invoice.IdFactura equals receivable.IdFactura
            where invoice.IdPedido == order.IdPedido
            select new
            {
                Invoice = invoice,
                Receivable = receivable
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (accounting is null)
        {
            return true;
        }

        payment.IdCxc = accounting.Receivable.IdCxc;
        if (accounting.Receivable.SaldoCxc == 0m)
        {
            return false;
        }

        if (accounting.Receivable.SaldoCxc != payment.MontoLocal)
        {
            return false;
        }

        var previousBalance = accounting.Receivable.SaldoCxc;
        var newBalance = previousBalance - payment.MontoLocal;
        accounting.Receivable.SaldoCxc = newBalance;
        accounting.Receivable.IdEstado = newBalance == 0m
            ? PaidReceivableStatusId
            : PartialReceivableStatusId;

        _dbContext.CuentasPorCobrarDetalle.Add(new CuentaPorCobrarMovimiento
        {
            IdCxc = accounting.Receivable.IdCxc,
            IdFactura = accounting.Invoice.IdFactura,
            MontoPagado = payment.MontoLocal,
            FechaPago = DateTime.UtcNow,
            TipoMovimiento = "Pago",
            SaldoAnterior = previousBalance,
            SaldoNuevo = newBalance,
            CodUsuario = accounting.Invoice.CodUsuario,
            CodTipoPago = payment.CodTipoPago,
            ReferenciaExterna = externalReference,
            Observaciones = $"Pago {payment.ProveedorPago} conciliado automáticamente."
        });

        return true;
    }

    private async Task<decimal?> GetReceivableBalanceAsync(
        int orderId,
        CancellationToken cancellationToken) =>
        await
        (
            from invoice in _dbContext.Facturas.AsNoTracking()
            join receivable in _dbContext.CuentasPorCobrar.AsNoTracking()
                on invoice.IdFactura equals receivable.IdFactura
            where invoice.IdPedido == orderId
            select (decimal?)receivable.SaldoCxc
        ).SingleOrDefaultAsync(cancellationToken);

    private async Task<RefundResponse> MapRefundAsync(
        ReembolsoPedido refund,
        CancellationToken cancellationToken)
    {
        var header = await
        (
            from salesReturn in _dbContext.Devoluciones.AsNoTracking()
            join invoice in _dbContext.Facturas.AsNoTracking()
                on salesReturn.IdFactura equals invoice.IdFactura
            where salesReturn.IdDevolucion == refund.IdDevolucion
            select new
            {
                InvoiceId = invoice.IdFactura,
                OrderId = invoice.IdPedido
            }
        ).SingleAsync(cancellationToken);

        return new(
            refund.IdReembolso,
            refund.IdDevolucion,
            header.InvoiceId,
            header.OrderId,
            refund.IdPago,
            refund.ProveedorPago,
            refund.Estado,
            refund.MontoLocal,
            refund.MonedaLocal,
            refund.MontoProveedor,
            refund.MonedaProveedor,
            refund.TipoCambio,
            refund.ReembolsoProveedorId,
            refund.ClaveIdempotencia,
            refund.ProcesadoPor,
            refund.CreadoElUtc,
            refund.CompletadoElUtc,
            refund.Observaciones,
            refund.DetalleFallo);
    }

    private string? ValidatePayPalConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            return "Configure PayPal:ClientId y PayPal:ClientSecret mediante User Secrets.";
        }

        if (_options.LocalCurrencyUnitsPerPayPalUnit <= 0m ||
            NormalizeCurrency(_options.LocalCurrency).Length != 3 ||
            NormalizeCurrency(_options.PayPalCurrency).Length != 3)
        {
            return "Revise las monedas y PayPal:LocalCurrencyUnitsPerPayPalUnit.";
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return "PayPal:BaseUrl debe ser una URL HTTPS válida.";
        }

        return null;
    }

    private decimal ToProviderAmount(decimal localAmount) =>
        decimal.Round(
            localAmount / _options.LocalCurrencyUnitsPerPayPalUnit,
            2,
            MidpointRounding.AwayFromZero);

    private static bool IsValidLocalAmount(decimal amount) =>
        amount > 0m && amount <= MaximumAmount;

    private static bool CanAccessOrder(
        Pedido order,
        string actorType,
        string actorReferenceId,
        bool isAdministrator) =>
        isAdministrator ||
        (actorType.Equals("Cliente", StringComparison.OrdinalIgnoreCase) &&
         order.CodCliente.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase)) ||
        (actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
         !string.IsNullOrWhiteSpace(order.CodRepartidor) &&
         order.CodRepartidor.Equals(actorReferenceId, StringComparison.OrdinalIgnoreCase));

    private static PaymentResponse MapPayment(PagoPedido payment, string orderCode) =>
        new(
            payment.IdPago,
            payment.IdPedido,
            orderCode,
            payment.IdCxc,
            payment.CodCliente,
            payment.CodTipoPago,
            payment.ProveedorPago,
            payment.Estado,
            payment.MontoLocal,
            payment.MonedaLocal,
            payment.MontoProveedor,
            payment.MonedaProveedor,
            payment.TipoCambio,
            payment.OrdenProveedorId,
            payment.CapturaProveedorId,
            payment.UrlAprobacion,
            payment.ClaveIdempotencia,
            payment.CreadoPorCuentaId,
            payment.CreadoElUtc,
            payment.CompletadoElUtc,
            payment.DetalleFallo);

    private static PaymentManagementResult NotFound(int orderId) =>
        Failure(
            PaymentManagementStatus.NotFound,
            $"No existe el pedido {orderId}.");

    private static PaymentManagementResult Failure(
        PaymentManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string value) =>
        value.Trim().ToUpperInvariant();

    private static bool CurrencyEquals(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        left.Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;
}
