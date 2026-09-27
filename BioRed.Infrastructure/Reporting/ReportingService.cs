using BioRed.Application.Reporting;
using BioRed.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Reporting;

public sealed class ReportingService : IReportingService
{
    private readonly BioRedDbContext _dbContext;

    public ReportingService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardReportResponse> GetDashboardAsync(
        DashboardReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var period = ResolvePeriod(request.FromUtc, request.ToUtc);
        var branchCode = Normalize(request.BranchCode);

        var orders = _dbContext.Pedidos
            .AsNoTracking()
            .Where(item =>
                item.FechaCreacion >= period.FromUtc &&
                item.FechaCreacion <= period.ToUtc &&
                (branchCode == null || item.CodSucursal == branchCode));

        var payments =
            from payment in _dbContext.PagosPedidos.AsNoTracking()
            join order in _dbContext.Pedidos.AsNoTracking()
                on payment.IdPedido equals order.IdPedido
            where
                payment.Estado == "COMPLETED" &&
                (payment.CompletadoElUtc ?? payment.CreadoElUtc) >= period.FromUtc &&
                (payment.CompletadoElUtc ?? payment.CreadoElUtc) <= period.ToUtc &&
                (branchCode == null || order.CodSucursal == branchCode)
            select payment;

        var refunds =
            from refund in _dbContext.ReembolsosPedidos.AsNoTracking()
            join payment in _dbContext.PagosPedidos.AsNoTracking()
                on refund.IdPago equals (long?)payment.IdPago
            join order in _dbContext.Pedidos.AsNoTracking()
                on payment.IdPedido equals order.IdPedido
            where
                refund.Estado == "COMPLETED" &&
                (refund.CompletadoElUtc ?? refund.CreadoElUtc) >= period.FromUtc &&
                (refund.CompletadoElUtc ?? refund.CreadoElUtc) <= period.ToUtc &&
                (branchCode == null || order.CodSucursal == branchCode)
            select refund;

        var totalOrders = await orders.CountAsync(cancellationToken);
        var deliveredOrders = await orders.CountAsync(
            item => item.EstadoPedido == "Entregado",
            cancellationToken);
        var cancelledOrders = await orders.CountAsync(
            item => item.EstadoPedido == "Cancelado",
            cancellationToken);
        var activeClients = await orders
            .Select(item => item.CodCliente)
            .Distinct()
            .CountAsync(cancellationToken);
        var completedPayments = await payments.CountAsync(cancellationToken);
        var grossSales = await payments
            .SumAsync(item => (decimal?)item.MontoLocal, cancellationToken) ?? 0m;
        var completedRefunds = await refunds
            .SumAsync(item => (decimal?)item.MontoLocal, cancellationToken) ?? 0m;
        var lowStockProducts = await _dbContext.Inventarios
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.CantidadActual <= item.StockMinimo &&
                    (branchCode == null || item.CodSucursal == branchCode),
                cancellationToken);

        return new DashboardReportResponse(
            period.FromUtc,
            period.ToUtc,
            branchCode,
            totalOrders,
            deliveredOrders,
            cancelledOrders,
            completedPayments,
            grossSales,
            completedRefunds,
            grossSales - completedRefunds,
            activeClients,
            lowStockProducts,
            DateTime.UtcNow);
    }

    public async Task<DailySalesReportResponse> GetDailySalesAsync(
        DailySalesReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var period = ResolvePeriod(request.FromUtc, request.ToUtc);
        var branchCode = Normalize(request.BranchCode);

        var orders = await _dbContext.Pedidos
            .AsNoTracking()
            .Where(item =>
                item.FechaCreacion >= period.FromUtc &&
                item.FechaCreacion <= period.ToUtc &&
                (branchCode == null || item.CodSucursal == branchCode))
            .GroupBy(item => item.FechaCreacion.Date)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var payments = await
        (
            from payment in _dbContext.PagosPedidos.AsNoTracking()
            join order in _dbContext.Pedidos.AsNoTracking()
                on payment.IdPedido equals order.IdPedido
            let registeredAtUtc = payment.CompletadoElUtc ?? payment.CreadoElUtc
            where
                payment.Estado == "COMPLETED" &&
                registeredAtUtc >= period.FromUtc &&
                registeredAtUtc <= period.ToUtc &&
                (branchCode == null || order.CodSucursal == branchCode)
            group payment by registeredAtUtc.Date into grouped
            select new
            {
                Date = grouped.Key,
                Count = grouped.Count(),
                Total = grouped.Sum(item => item.MontoLocal)
            }
        ).ToListAsync(cancellationToken);

        var refunds = await
        (
            from refund in _dbContext.ReembolsosPedidos.AsNoTracking()
            join payment in _dbContext.PagosPedidos.AsNoTracking()
                on refund.IdPago equals (long?)payment.IdPago
            join order in _dbContext.Pedidos.AsNoTracking()
                on payment.IdPedido equals order.IdPedido
            let registeredAtUtc = refund.CompletadoElUtc ?? refund.CreadoElUtc
            where
                refund.Estado == "COMPLETED" &&
                registeredAtUtc >= period.FromUtc &&
                registeredAtUtc <= period.ToUtc &&
                (branchCode == null || order.CodSucursal == branchCode)
            group refund by registeredAtUtc.Date into grouped
            select new
            {
                Date = grouped.Key,
                Total = grouped.Sum(item => item.MontoLocal)
            }
        ).ToListAsync(cancellationToken);

        var allDates = orders.Select(item => item.Date)
            .Concat(payments.Select(item => item.Date))
            .Concat(refunds.Select(item => item.Date))
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        var items = allDates.Select(date =>
        {
            var order = orders.SingleOrDefault(item => item.Date == date);
            var payment = payments.SingleOrDefault(item => item.Date == date);
            var refund = refunds.SingleOrDefault(item => item.Date == date);
            var gross = payment?.Total ?? 0m;
            var refunded = refund?.Total ?? 0m;

            return new DailySalesItemResponse(
                DateOnly.FromDateTime(date),
                order?.Count ?? 0,
                payment?.Count ?? 0,
                gross,
                refunded,
                gross - refunded);
        }).ToArray();

        return new DailySalesReportResponse(
            period.FromUtc,
            period.ToUtc,
            branchCode,
            items,
            DateTime.UtcNow);
    }

    public async Task<LowStockReportResponse> GetLowStockAsync(
        LowStockReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var branchCode = Normalize(request.BranchCode);

        var query =
            from inventory in _dbContext.Inventarios.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on inventory.CodProducto equals product.CodProducto
            join branch in _dbContext.Sucursales.AsNoTracking()
                on inventory.CodSucursal equals branch.CodSucursal
            where
                inventory.CantidadActual <= inventory.StockMinimo &&
                (branchCode == null || inventory.CodSucursal == branchCode)
            orderby inventory.CantidadActual, product.NombreProducto
            select new LowStockItemResponse(
                inventory.IdInventario,
                inventory.CodSucursal,
                branch.NombreSucursal,
                inventory.CodProducto,
                product.NombreProducto,
                inventory.CantidadActual,
                inventory.StockMinimo,
                inventory.StockMinimo - inventory.CantidadActual,
                inventory.UltimaActualizacion);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new LowStockReportResponse(
            request.Page,
            request.PageSize,
            totalItems,
            totalPages,
            items);
    }

    public async Task<AuditReportResponse> GetAuditAsync(
        AuditReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var period = ResolvePeriod(request.FromUtc, request.ToUtc);
        var module = Normalize(request.Module)?.ToLowerInvariant();
        var referenceId = Normalize(request.ReferenceId);
        var httpMethod = Normalize(request.HttpMethod)?.ToUpperInvariant();

        var query = _dbContext.BitacoraApi
            .AsNoTracking()
            .Where(item =>
                item.FechaUtc >= period.FromUtc &&
                item.FechaUtc <= period.ToUtc &&
                (module == null || item.Modulo == module) &&
                (referenceId == null || item.ReferenciaId == referenceId) &&
                (!request.StatusCode.HasValue || item.CodigoEstado == request.StatusCode.Value) &&
                (httpMethod == null || item.MetodoHttp == httpMethod));

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);
        var items = await query
            .OrderByDescending(item => item.FechaUtc)
            .ThenByDescending(item => item.IdBitacora)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new AuditItemResponse(
                item.IdBitacora,
                item.IdCuenta,
                item.TipoUsuario,
                item.ReferenciaId,
                item.Modulo,
                item.MetodoHttp,
                item.Ruta,
                item.CodigoEstado,
                item.DuracionMs,
                item.DireccionIp,
                item.CorrelationId,
                item.FechaUtc))
            .ToListAsync(cancellationToken);

        return new AuditReportResponse(
            request.Page,
            request.PageSize,
            totalItems,
            totalPages,
            items);
    }

    private static (DateTime FromUtc, DateTime ToUtc) ResolvePeriod(
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var nowUtc = DateTime.UtcNow;
        var resolvedToUtc = EnsureUtc(toUtc ?? nowUtc);
        var resolvedFromUtc = EnsureUtc(fromUtc ?? resolvedToUtc.AddDays(-30));
        return (resolvedFromUtc, resolvedToUtc);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
