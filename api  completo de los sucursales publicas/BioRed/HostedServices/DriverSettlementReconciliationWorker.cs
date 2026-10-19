using BioRed.Application.Payments;
using BioRed.Infrastructure.Payments;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BioRed.HostedServices;

/// <summary>
/// Actualiza en segundo plano las liquidaciones que PayPal procesa de forma
/// asíncrona. Nunca modifica pedidos ni vuelve a crear el pago.
/// </summary>
public sealed class DriverSettlementReconciliationWorker : BackgroundService
{
    private const string PayPalSource = "PAYPAL";
    private const string ProcessingStatus = "PROCESANDO";
    private const string PendingConfigurationStatus = "PENDIENTE_CONFIGURACION";
    private const string CompletedPaymentStatus = "COMPLETED";
    private const string DeliveredOrderStatus = "Entregado";
    private const string PayPalPaymentCode = "PAGO-PAYPAL";
    private const string CashPaymentCode = "PAGO-EFECTIVO";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<DriverSettlementOptions> _options;
    private readonly ILogger<DriverSettlementReconciliationWorker> _logger;

    public DriverSettlementReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<DriverSettlementOptions> options,
        ILogger<DriverSettlementReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var intervalSeconds = Math.Clamp(
                options.ReconciliationIntervalSeconds,
                30,
                3600);

            if (options.Enabled)
            {
                try
                {
                    await ReconcileAsync(
                        TimeSpan.FromSeconds(intervalSeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Falló el ciclo de conciliación de liquidaciones PayPal.");
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(intervalSeconds),
                stoppingToken);
        }
    }

    private async Task ReconcileAsync(
        TimeSpan minimumAge,
        CancellationToken cancellationToken)
    {
        await CreateMissingSettlementsAsync(cancellationToken);

        long[] settlementIds;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<BioRedDbContext>();
            var retryBeforeUtc = DateTime.UtcNow.Subtract(minimumAge);
            settlementIds = await dbContext.LiquidacionesRepartidor
                .AsNoTracking()
                .Where(item =>
                    item.MetodoOrigen == PayPalSource &&
                    ((item.Estado == ProcessingStatus &&
                      item.LoteProveedorId != null &&
                      (item.UltimoIntentoUtc == null ||
                       item.UltimoIntentoUtc <= retryBeforeUtc)) ||
                     (item.Estado == PendingConfigurationStatus &&
                      dbContext.CuentasPagoRepartidor.Any(account =>
                          account.CodRepartidor == item.CodRepartidor &&
                          account.Activa))))
                .OrderBy(item => item.UltimoIntentoUtc)
                .ThenBy(item => item.IdLiquidacion)
                .Select(item => item.IdLiquidacion)
                .Take(50)
                .ToArrayAsync(cancellationToken);
        }

        foreach (var settlementId in settlementIds)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IDriverSettlementService>();
                await service.RefreshPayPalAsync(
                    settlementId,
                    "Empleado",
                    "SYSTEM",
                    true,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "No pudo conciliarse la liquidación PayPal {SettlementId}.",
                    settlementId);
            }
        }
    }

    private async Task CreateMissingSettlementsAsync(
        CancellationToken cancellationToken)
    {
        MissingSettlement[] missing;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<BioRedDbContext>();
            missing = await
            (
                from payment in dbContext.PagosPedidos.AsNoTracking()
                join order in dbContext.Pedidos.AsNoTracking()
                    on payment.IdPedido equals order.IdPedido
                where payment.Estado == CompletedPaymentStatus &&
                      order.EstadoPedido == DeliveredOrderStatus &&
                      order.CodRepartidor != null &&
                      (order.CodTipoPago == PayPalPaymentCode ||
                       order.CodTipoPago == CashPaymentCode) &&
                      !dbContext.LiquidacionesRepartidor.Any(settlement =>
                          settlement.IdPedido == order.IdPedido)
                orderby payment.IdPago
                select new MissingSettlement(
                    order.IdPedido,
                    payment.IdPago,
                    order.CodTipoPago)
            )
            .Take(50)
            .ToArrayAsync(cancellationToken);
        }

        foreach (var item in missing)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider
                    .GetRequiredService<IDriverSettlementService>();

                if (item.PaymentTypeCode == PayPalPaymentCode)
                {
                    await service.EnsurePayPalSettlementAsync(
                        item.OrderId,
                        cancellationToken);
                }
                else
                {
                    await service.EnsureCashSettlementAsync(
                        item.PaymentId,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "No pudo recuperarse la liquidación faltante del pedido {OrderId}.",
                    item.OrderId);
            }
        }
    }

    private sealed record MissingSettlement(
        int OrderId,
        long PaymentId,
        string PaymentTypeCode);
}
