using System.Data;
using BioRed.Application.Accounting;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Accounting;

public sealed class BillingManagementService : IBillingManagementService
{
    private const int IssuedInvoiceStatusId = 10;
    private const int PendingReceivableStatusId = 30;
    private const int PaidReceivableStatusId = 31;
    private const decimal MaximumAmount = 9_999_999_999_999_999.99m;
    private readonly BioRedDbContext _dbContext;

    public BillingManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedInvoicesResponse> GetAsync(
        GetManagedInvoicesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Facturas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = NormalizeCode(request.BranchCode);
            query = query.Where(item => item.CodSucursal == branchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.ClientCode))
        {
            var clientCode = NormalizeCode(request.ClientCode);
            query = query.Where(item => item.CodCliente == clientCode);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await
        (
            from invoice in query
            join client in _dbContext.Clientes.AsNoTracking()
                on invoice.CodCliente equals client.CodCliente
            join status in _dbContext.Estados.AsNoTracking()
                on invoice.IdEstado equals status.IdEstado
            join receivable in _dbContext.CuentasPorCobrar.AsNoTracking()
                on invoice.IdFactura equals receivable.IdFactura
            orderby invoice.FechaFactura descending, invoice.IdFactura descending
            select new
            {
                Invoice = invoice,
                ClientName = client.NombreCliente + " " + client.ApellidoCliente,
                Status = status.NombreEstado,
                Balance = receivable.SaldoCxc
            }
        )
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToArrayAsync(cancellationToken);

        var items = rows.Select(row => new ManagedInvoiceSummaryResponse(
            row.Invoice.IdFactura,
            row.Invoice.IdPedido,
            BuildDocument(row.Invoice.SerieDocumento, row.Invoice.NumeroDocumento),
            row.Invoice.CodCliente,
            row.ClientName,
            row.Invoice.CodSucursal,
            row.Invoice.FechaFactura,
            row.Invoice.TotalFactura,
            row.Invoice.IdEstado,
            row.Status,
            row.Balance)).ToArray();

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(request.Page, request.PageSize, totalItems, totalPages, items);
    }

    public async Task<BillingManagementResult> GetByIdAsync(
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        var response = await BuildResponseAsync(invoiceId, cancellationToken);
        return response is null
            ? Failure(BillingManagementStatus.NotFound, $"No existe la factura {invoiceId}.")
            : new(BillingManagementStatus.Success, response);
    }

    public async Task<BillingManagementResult> CreateFromOrderAsync(
        string actorReferenceId,
        CreateManagedInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        var series = CleanOptional(request.DocumentSeries)?.ToUpperInvariant();
        var number = request.DocumentNumber.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = request.DueDate ?? today;

        if (dueDate < today)
        {
            return Failure(
                BillingManagementStatus.InvalidDueDate,
                "La fecha de vencimiento no puede estar en el pasado.");
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();

                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                var userExists = await _dbContext.Usuarios.AsNoTracking().AnyAsync(
                    item => item.CodUsuario == actorCode && item.Estado,
                    cancellationToken);

                if (!userExists)
                {
                    return Failure(
                        BillingManagementStatus.UserNotFound,
                        "La cuenta autenticada no está vinculada con un usuario activo.");
                }

                var configuredStatusIds = await _dbContext.Estados.AsNoTracking()
                    .Where(item =>
                        (item.IdEstado == IssuedInvoiceStatusId ||
                         item.IdEstado == PendingReceivableStatusId ||
                         item.IdEstado == PaidReceivableStatusId) &&
                        item.Activo)
                    .Select(item => item.IdEstado)
                    .ToArrayAsync(cancellationToken);

                if (configuredStatusIds.Length != 3)
                {
                    return Failure(
                        BillingManagementStatus.StatusNotConfigured,
                        "No están configurados los estados de factura y cuenta por cobrar.");
                }

                var order = await _dbContext.Pedidos
                    .Include(item => item.Detalles)
                    .SingleOrDefaultAsync(
                        item => item.IdPedido == request.OrderId,
                        cancellationToken);

                if (order is null)
                {
                    return Failure(
                        BillingManagementStatus.OrderNotFound,
                        $"No existe el pedido {request.OrderId}.");
                }

                if (order.EstadoPedido.Equals("Cancelado", StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(
                        BillingManagementStatus.OrderCancelled,
                        "No se puede facturar un pedido cancelado.");
                }

                if (!order.EstadoPedido.Equals(
                        "Entregado",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(
                        BillingManagementStatus.OrderNotDelivered,
                        "La factura solamente puede emitirse después de entregar el pedido.");
                }

                if (await _dbContext.Facturas.AsNoTracking().AnyAsync(
                        item => item.IdPedido == request.OrderId,
                        cancellationToken))
                {
                    return Failure(
                        BillingManagementStatus.DuplicateOrder,
                        "El pedido ya tiene una factura.");
                }

                if (await _dbContext.Facturas.AsNoTracking().AnyAsync(
                        item => item.CodSucursal == order.CodSucursal &&
                                item.SerieDocumento == series &&
                                item.NumeroDocumento == number,
                        cancellationToken))
                {
                    return Failure(
                        BillingManagementStatus.DuplicateDocument,
                        "Ya existe ese documento para la sucursal.");
                }

                var expectedTotal = decimal.Round(
                    order.Subtotal + order.CostoEnvio - (order.DescuentoAplicado ?? 0m),
                    2,
                    MidpointRounding.AwayFromZero);

                if (order.Detalles.Count == 0 ||
                    order.Detalles.Any(item => item.Cantidad <= 0 || item.PrecioUnitario <= 0m) ||
                    order.Subtotal < 0m ||
                    order.CostoEnvio < 0m ||
                    (order.DescuentoAplicado ?? 0m) < 0m ||
                    order.TotalCobrado <= 0m ||
                    order.TotalCobrado > MaximumAmount ||
                    order.TotalCobrado != expectedTotal)
                {
                    return Failure(
                        BillingManagementStatus.InvalidTotal,
                        "El pedido no tiene un detalle o total válido para facturación.");
                }

                var productCodes = order.Detalles
                    .Select(item => item.CodProducto)
                    .Distinct()
                    .ToArray();
                var productCosts = await _dbContext.Productos.AsNoTracking()
                    .Where(item => productCodes.Contains(item.CodProducto))
                    .ToDictionaryAsync(
                        item => item.CodProducto,
                        item => item.PrecioCosto,
                        cancellationToken);

                if (productCosts.Count != productCodes.Length ||
                    productCosts.Values.Any(cost => cost < 0m))
                {
                    return Failure(
                        BillingManagementStatus.OrderNotFound,
                        "Uno de los productos del pedido ya no existe.");
                }

                var invoiceDateUtc = DateTime.UtcNow;
                var invoice = new FacturaEncabezadoContable
                {
                    CodCliente = order.CodCliente,
                    CodSucursal = order.CodSucursal,
                    CodUsuario = actorCode,
                    CodTipoPago = order.CodTipoPago,
                    FechaFactura = invoiceDateUtc,
                    TotalFactura = order.TotalCobrado,
                    IdEstado = IssuedInvoiceStatusId,
                    IdPedido = order.IdPedido,
                    SerieDocumento = series,
                    NumeroDocumento = number,
                    Subtotal = order.Subtotal,
                    CostoEnvio = order.CostoEnvio,
                    Descuento = order.DescuentoAplicado ?? 0m,
                    Observaciones = CleanOptional(request.Observations)
                };

                foreach (var orderDetail in order.Detalles)
                {
                    invoice.Detalles.Add(new FacturaDetalleContable
                    {
                        CodProducto = orderDetail.CodProducto,
                        Cantidad = orderDetail.Cantidad,
                        PrecioCosto = productCosts[orderDetail.CodProducto],
                        PrecioVenta = orderDetail.PrecioUnitario
                    });
                }

                _dbContext.Facturas.Add(invoice);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var receivable = new CuentaPorCobrar
                {
                    CodCliente = order.CodCliente,
                    CodUsuario = actorCode,
                    CodSucursal = order.CodSucursal,
                    FechaCxc = invoiceDateUtc,
                    TotalCxc = order.TotalCobrado,
                    IdEstado = PendingReceivableStatusId,
                    IdFactura = invoice.IdFactura,
                    SaldoCxc = order.TotalCobrado,
                    FechaVencimiento = dueDate
                };

                _dbContext.CuentasPorCobrar.Add(receivable);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var completedPayment = await _dbContext.PagosPedidos
                    .SingleOrDefaultAsync(
                        item => item.IdPedido == order.IdPedido &&
                                item.Estado == "COMPLETED",
                        cancellationToken);

                if (completedPayment is not null)
                {
                    if (completedPayment.MontoLocal != receivable.SaldoCxc)
                    {
                        return Failure(
                            BillingManagementStatus.InvalidTotal,
                            "El pago completado no coincide con el total de la factura.");
                    }

                    var previousBalance = receivable.SaldoCxc;
                    receivable.SaldoCxc = 0m;
                    receivable.IdEstado = PaidReceivableStatusId;
                    completedPayment.IdCxc = receivable.IdCxc;

                    _dbContext.CuentasPorCobrarDetalle.Add(
                        new CuentaPorCobrarMovimiento
                        {
                            IdCxc = receivable.IdCxc,
                            IdFactura = invoice.IdFactura,
                            MontoPagado = completedPayment.MontoLocal,
                            FechaPago = completedPayment.CompletadoElUtc ?? invoiceDateUtc,
                            TipoMovimiento = "Pago",
                            SaldoAnterior = previousBalance,
                            SaldoNuevo = 0m,
                            CodUsuario = actorCode,
                            CodTipoPago = completedPayment.CodTipoPago,
                            ReferenciaExterna =
                                completedPayment.CapturaProveedorId ??
                                completedPayment.ReferenciaExterna,
                            Observaciones =
                                $"Pago {completedPayment.ProveedorPago} conciliado al emitir la factura."
                        });

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                var response = await BuildResponseAsync(
                    invoice.IdFactura,
                    cancellationToken);

                if (response is null)
                {
                    return Failure(
                        BillingManagementStatus.InvalidTotal,
                        "No fue posible verificar la factura antes de confirmar la transacción.");
                }

                await transaction.CommitAsync(cancellationToken);

                return new BillingManagementResult(
                    BillingManagementStatus.Success,
                    response);
            });
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                BillingManagementStatus.DuplicateDocument,
                "El pedido o documento ya fue facturado.");
        }
    }

    private async Task<ManagedInvoiceResponse?> BuildResponseAsync(
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var header = await
        (
            from invoice in _dbContext.Facturas.AsNoTracking()
            join order in _dbContext.Pedidos.AsNoTracking()
                on invoice.IdPedido equals order.IdPedido
            join client in _dbContext.Clientes.AsNoTracking()
                on invoice.CodCliente equals client.CodCliente
            join branch in _dbContext.Sucursales.AsNoTracking()
                on invoice.CodSucursal equals branch.CodSucursal
            join paymentType in _dbContext.TiposPago.AsNoTracking()
                on invoice.CodTipoPago equals paymentType.CodTipoPago
            join status in _dbContext.Estados.AsNoTracking()
                on invoice.IdEstado equals status.IdEstado
            join receivable in _dbContext.CuentasPorCobrar.AsNoTracking()
                on invoice.IdFactura equals receivable.IdFactura
            where invoice.IdFactura == invoiceId
            select new
            {
                Invoice = invoice,
                Order = order,
                ClientName = client.NombreCliente + " " + client.ApellidoCliente,
                BranchName = branch.NombreSucursal,
                PaymentTypeName = paymentType.NombreTipoPago,
                Status = status.NombreEstado,
                Receivable = receivable
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var items = await
        (
            from detail in _dbContext.FacturasDetalle.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdFactura == invoiceId
            orderby detail.IdFacturaDetalle
            select new ManagedInvoiceItemResponse(
                detail.IdFacturaDetalle,
                detail.CodProducto,
                product.NombreProducto,
                detail.Cantidad,
                detail.PrecioCosto,
                detail.PrecioVenta,
                detail.PrecioVenta * detail.Cantidad)
        ).ToArrayAsync(cancellationToken);

        return new ManagedInvoiceResponse(
            header.Invoice.IdFactura,
            header.Invoice.IdPedido,
            header.Order.CodPedido,
            header.Invoice.SerieDocumento ?? string.Empty,
            header.Invoice.NumeroDocumento,
            header.Invoice.CodCliente,
            header.ClientName,
            header.Invoice.CodSucursal,
            header.BranchName,
            header.Invoice.CodUsuario,
            header.Invoice.CodTipoPago,
            header.PaymentTypeName,
            header.Invoice.FechaFactura,
            header.Invoice.Subtotal,
            header.Invoice.CostoEnvio,
            header.Invoice.Descuento,
            header.Invoice.TotalFactura,
            header.Invoice.IdEstado,
            header.Status,
            header.Invoice.Observaciones,
            header.Receivable.IdCxc,
            header.Receivable.SaldoCxc,
            header.Receivable.FechaVencimiento,
            items);
    }

    private static BillingManagementResult Failure(
        BillingManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildDocument(string? series, string number) =>
        string.IsNullOrWhiteSpace(series) ? number : $"{series}-{number}";

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;
}
