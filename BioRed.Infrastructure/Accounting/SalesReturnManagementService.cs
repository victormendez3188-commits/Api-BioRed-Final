using System.Data;
using BioRed.Application.Accounting;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Accounting;

public sealed class SalesReturnManagementService : ISalesReturnManagementService
{
    private const int AppliedReturnStatusId = 20;
    private const int SettledReceivableStatusId = 31;
    private const int PartialReceivableStatusId = 32;
    private const decimal MaximumAmount = 9_999_999_999_999_999.99m;
    private readonly BioRedDbContext _dbContext;

    public SalesReturnManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedReturnsResponse> GetAsync(
        GetManagedReturnsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Devoluciones.AsNoTracking().AsQueryable();

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
        var items = await
        (
            from salesReturn in query
            join status in _dbContext.Estados.AsNoTracking()
                on salesReturn.IdEstado equals status.IdEstado
            orderby salesReturn.FechaDevolucion descending, salesReturn.IdDevolucion descending
            select new ManagedReturnSummaryResponse(
                salesReturn.IdDevolucion,
                salesReturn.IdFactura,
                salesReturn.NumeroDevolucion,
                salesReturn.CodCliente,
                salesReturn.CodSucursal,
                salesReturn.FechaDevolucion,
                salesReturn.TotalDevolucion,
                salesReturn.CreditoAplicado,
                salesReturn.ReembolsoPendiente,
                status.NombreEstado)
        )
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToArrayAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(request.Page, request.PageSize, totalItems, totalPages, items);
    }

    public async Task<SalesReturnManagementResult> GetByIdAsync(
        int returnId,
        CancellationToken cancellationToken = default)
    {
        var response = await BuildResponseAsync(returnId, cancellationToken);
        return response is null
            ? Failure(SalesReturnManagementStatus.NotFound, $"No existe la devolución {returnId}.")
            : new(SalesReturnManagementStatus.Success, response);
    }

    public async Task<SalesReturnManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        var requestedItems = request.Items.Select(item => new NormalizedReturnItem(
            NormalizeCode(item.ProductCode),
            item.Quantity,
            CleanOptional(item.LotNumber)?.ToUpperInvariant())).ToArray();

        if (requestedItems.GroupBy(item => item.ProductCode).Any(group => group.Count() > 1))
        {
            return Failure(
                SalesReturnManagementStatus.DuplicateProduct,
                "No puede repetir un producto dentro de la misma devolución.");
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

                if (!await _dbContext.Usuarios.AsNoTracking().AnyAsync(
                        item => item.CodUsuario == actorCode && item.Estado,
                        cancellationToken))
                {
                    return Failure(
                        SalesReturnManagementStatus.UserNotFound,
                        "La cuenta autenticada no está vinculada con un usuario activo.");
                }

                var statusCount = await _dbContext.Estados.AsNoTracking().CountAsync(
                    item =>
                        (item.IdEstado == AppliedReturnStatusId ||
                         item.IdEstado == SettledReceivableStatusId ||
                         item.IdEstado == PartialReceivableStatusId) &&
                        item.Activo,
                    cancellationToken);

                if (statusCount != 3)
                {
                    return Failure(
                        SalesReturnManagementStatus.StatusNotConfigured,
                        "No están configurados los estados necesarios para devoluciones.");
                }

                var invoice = await _dbContext.Facturas
                    .Include(item => item.Detalles)
                    .SingleOrDefaultAsync(
                        item => item.IdFactura == request.InvoiceId,
                        cancellationToken);

                if (invoice is null)
                {
                    return Failure(
                        SalesReturnManagementStatus.InvoiceNotFound,
                        $"No existe la factura {request.InvoiceId}.");
                }

                var order = await _dbContext.Pedidos.AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.IdPedido == invoice.IdPedido,
                        cancellationToken);

                if (order is null || order.EstadoPedido != "Entregado")
                {
                    return Failure(
                        SalesReturnManagementStatus.OrderNotDelivered,
                        "Solo se aceptan devoluciones de pedidos Entregados.");
                }

                var invoiceDetails = invoice.Detalles.ToDictionary(
                    item => item.CodProducto,
                    StringComparer.OrdinalIgnoreCase);

                var returnedRows = await
                (
                    from previousReturn in _dbContext.Devoluciones.AsNoTracking()
                    join detail in _dbContext.DevolucionesDetalle.AsNoTracking()
                        on previousReturn.IdDevolucion equals detail.IdDevolucion
                    where previousReturn.IdFactura == invoice.IdFactura
                    select new { detail.CodProducto, detail.Cantidad }
                ).ToArrayAsync(cancellationToken);

                var returnedByProduct = returnedRows
                    .GroupBy(item => item.CodProducto, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(item => item.Cantidad),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var item in requestedItems)
                {
                    if (!invoiceDetails.TryGetValue(item.ProductCode, out var invoiceDetail))
                    {
                        return Failure(
                            SalesReturnManagementStatus.ProductNotInInvoice,
                            $"El producto {item.ProductCode} no pertenece a la factura.");
                    }

                    returnedByProduct.TryGetValue(item.ProductCode, out var alreadyReturned);
                    if (item.Quantity > invoiceDetail.Cantidad - alreadyReturned)
                    {
                        return Failure(
                            SalesReturnManagementStatus.QuantityExceeded,
                            $"La cantidad de {item.ProductCode} supera lo disponible para devolución.");
                    }
                }

                var productCodes = requestedItems.Select(item => item.ProductCode).ToArray();
                var products = await _dbContext.Productos.AsNoTracking()
                    .Where(item => productCodes.Contains(item.CodProducto))
                    .ToDictionaryAsync(
                        item => item.CodProducto,
                        item => item.NombreProducto,
                        cancellationToken);

                if (products.Count != productCodes.Length)
                {
                    return Failure(
                        SalesReturnManagementStatus.ProductNotInInvoice,
                        "Uno de los productos ya no existe.");
                }

                var inventories = await _dbContext.Inventarios
                    .Where(item =>
                        item.CodSucursal == invoice.CodSucursal &&
                        productCodes.Contains(item.CodProducto))
                    .ToArrayAsync(cancellationToken);
                var inventoryByProduct = inventories.ToDictionary(
                    item => item.CodProducto,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in requestedItems)
                {
                    if (!inventoryByProduct.TryGetValue(item.ProductCode, out var inventory))
                    {
                        return Failure(
                            SalesReturnManagementStatus.ProductNotInInvoice,
                            $"No existe inventario de {item.ProductCode} en la sucursal.");
                    }

                    var resultingQuantity =
                        (long)inventory.CantidadActual + item.Quantity;
                    var allowedMaximum = inventory.StockMaximo > 0
                        ? inventory.StockMaximo
                        : int.MaxValue;

                    if (resultingQuantity > allowedMaximum)
                    {
                        return Failure(
                            SalesReturnManagementStatus.InventoryLimitExceeded,
                            $"La devolución supera el stock máximo de {item.ProductCode}.");
                    }
                }

                var requestedLots = requestedItems
                    .Where(item => item.LotNumber is not null)
                    .ToArray();
                var lotNumbers = requestedLots.Select(item => item.LotNumber!).ToArray();
                var lots = lotNumbers.Length == 0
                    ? Array.Empty<Lote>()
                    : await _dbContext.Lotes.Where(item =>
                            productCodes.Contains(item.CodProducto) &&
                            lotNumbers.Contains(item.NumeroLote))
                        .ToArrayAsync(cancellationToken);

                foreach (var item in requestedLots)
                {
                    if (!lots.Any(lot =>
                            lot.CodProducto == item.ProductCode &&
                            lot.NumeroLote == item.LotNumber))
                    {
                        return Failure(
                            SalesReturnManagementStatus.LotNotFound,
                            $"No existe el lote {item.LotNumber} para {item.ProductCode}.");
                    }
                }

                var grossSubtotal = invoice.Detalles.Sum(item =>
                    item.PrecioVenta * item.Cantidad);
                grossSubtotal = decimal.Round(
                    grossSubtotal,
                    2,
                    MidpointRounding.AwayFromZero);

                if (grossSubtotal != invoice.Subtotal ||
                    invoice.Descuento < 0m ||
                    invoice.Descuento > invoice.Subtotal)
                {
                    return Failure(
                        SalesReturnManagementStatus.InvalidTotal,
                        "La factura no conserva un subtotal y descuento válidos para calcular la devolución.");
                }

                var promotionProductCodes = string.IsNullOrWhiteSpace(order.CodPromocion)
                    ? Array.Empty<string>()
                    : await _dbContext.PromocionesProductos.AsNoTracking()
                        .Where(item => item.CodPromocion == order.CodPromocion)
                        .Select(item => item.CodProducto)
                        .ToArrayAsync(cancellationToken);
                var eligibleProductCodes = promotionProductCodes.Length == 0
                    ? invoice.Detalles.Select(item => item.CodProducto).ToHashSet(
                        StringComparer.OrdinalIgnoreCase)
                    : promotionProductCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var eligibleGross = invoice.Detalles
                    .Where(item => eligibleProductCodes.Contains(item.CodProducto))
                    .Sum(item => item.PrecioVenta * item.Cantidad);

                if (invoice.Descuento > eligibleGross)
                {
                    return Failure(
                        SalesReturnManagementStatus.InvalidTotal,
                        "El descuento de la factura supera el monto de los productos promocionados.");
                }

                var netLineAmounts = AllocateNetLineAmounts(
                    invoice.Detalles,
                    eligibleProductCodes,
                    invoice.Descuento,
                    eligibleGross);
                var returnLineAmounts = new Dictionary<string, decimal>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in requestedItems)
                {
                    var invoiceDetail = invoiceDetails[item.ProductCode];
                    returnedByProduct.TryGetValue(item.ProductCode, out var alreadyReturned);
                    var previousAmount = decimal.Round(
                        netLineAmounts[item.ProductCode] * alreadyReturned /
                        invoiceDetail.Cantidad,
                        2,
                        MidpointRounding.AwayFromZero);
                    var cumulativeAmount = decimal.Round(
                        netLineAmounts[item.ProductCode] *
                        (alreadyReturned + item.Quantity) /
                        invoiceDetail.Cantidad,
                        2,
                        MidpointRounding.AwayFromZero);
                    returnLineAmounts[item.ProductCode] = cumulativeAmount - previousAmount;
                }

                var total = decimal.Round(
                    returnLineAmounts.Values.Sum(),
                    2,
                    MidpointRounding.AwayFromZero);

                if (total <= 0m || total > MaximumAmount)
                {
                    return Failure(
                        SalesReturnManagementStatus.InvalidTotal,
                        "El total calculado de la devolución no es válido.");
                }

                var receivable = await _dbContext.CuentasPorCobrar.SingleOrDefaultAsync(
                    item => item.IdFactura == invoice.IdFactura,
                    cancellationToken);
                var creditApplied = receivable is null
                    ? 0m
                    : Math.Min(receivable.SaldoCxc, total);
                var refundPending = total - creditApplied;
                var nowUtc = DateTime.UtcNow;

                var salesReturn = new DevolucionEncabezadoContable
                {
                    CodCliente = invoice.CodCliente,
                    CodSucursal = invoice.CodSucursal,
                    CodUsuario = actorCode,
                    CodTipoPago = invoice.CodTipoPago,
                    FechaDevolucion = nowUtc,
                    TotalDevolucion = total,
                    IdEstado = AppliedReturnStatusId,
                    IdFactura = invoice.IdFactura,
                    NumeroDevolucion = CreateReturnNumber(),
                    Motivo = request.Reason.Trim(),
                    CreditoAplicado = creditApplied,
                    ReembolsoPendiente = refundPending
                };

                foreach (var item in requestedItems)
                {
                    var invoiceDetail = invoiceDetails[item.ProductCode];
                    salesReturn.Detalles.Add(new DevolucionDetalleContable
                    {
                        CodProducto = item.ProductCode,
                        Cantidad = item.Quantity,
                        PrecioCosto = invoiceDetail.PrecioCosto,
                        PrecioVenta = invoiceDetail.PrecioVenta,
                        MontoLinea = returnLineAmounts[item.ProductCode],
                        NumeroLote = item.LotNumber
                    });

                    var inventory = inventoryByProduct[item.ProductCode];
                    inventory.CantidadActual += item.Quantity;
                    inventory.UltimaActualizacion = nowUtc;

                    if (item.LotNumber is not null)
                    {
                        var lot = lots.Single(existing =>
                            existing.CodProducto == item.ProductCode &&
                            existing.NumeroLote == item.LotNumber);
                        lot.CantidadLote += item.Quantity;
                    }
                }

                _dbContext.Devoluciones.Add(salesReturn);
                await _dbContext.SaveChangesAsync(cancellationToken);

                foreach (var item in requestedItems)
                {
                    _dbContext.Kardex.Add(new KardexMovimiento
                    {
                        CodProducto = item.ProductCode,
                        CodSucursal = invoice.CodSucursal,
                        TipoMovimiento = "DevolucionVenta",
                        Cantidad = item.Quantity,
                        FechaMovimiento = nowUtc,
                        ReferenciaId = salesReturn.IdDevolucion,
                        Observacion = Limit(
                            $"Entrada por devolución {salesReturn.NumeroDevolucion}. Motivo: {salesReturn.Motivo}",
                            200)
                    });
                }

                if (receivable is not null && creditApplied > 0m)
                {
                    var previousBalance = receivable.SaldoCxc;
                    var newBalance = previousBalance - creditApplied;
                    receivable.SaldoCxc = newBalance;
                    receivable.IdEstado = newBalance == 0m
                        ? SettledReceivableStatusId
                        : PartialReceivableStatusId;

                    _dbContext.CuentasPorCobrarDetalle.Add(new CuentaPorCobrarMovimiento
                    {
                        IdCxc = receivable.IdCxc,
                        IdFactura = invoice.IdFactura,
                        MontoPagado = creditApplied,
                        FechaPago = nowUtc,
                        TipoMovimiento = "NotaCredito",
                        SaldoAnterior = previousBalance,
                        SaldoNuevo = newBalance,
                        CodUsuario = actorCode,
                        CodTipoPago = null,
                        Observaciones = $"Crédito por devolución {salesReturn.NumeroDevolucion}.",
                        ReferenciaExterna = salesReturn.NumeroDevolucion
                    });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = await BuildResponseAsync(
                    salesReturn.IdDevolucion,
                    cancellationToken);

                if (response is null)
                {
                    return Failure(
                        SalesReturnManagementStatus.InvalidTotal,
                        "No fue posible verificar la devolución antes de confirmar la transacción.");
                }

                await transaction.CommitAsync(cancellationToken);

                return new SalesReturnManagementResult(
                    SalesReturnManagementStatus.Success,
                    response);
            });
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                SalesReturnManagementStatus.InvalidTotal,
                "No fue posible registrar la devolución porque uno de sus datos ya existe.");
        }
    }

    private async Task<ManagedReturnResponse?> BuildResponseAsync(
        int returnId,
        CancellationToken cancellationToken)
    {
        var header = await
        (
            from salesReturn in _dbContext.Devoluciones.AsNoTracking()
            join client in _dbContext.Clientes.AsNoTracking()
                on salesReturn.CodCliente equals client.CodCliente
            join status in _dbContext.Estados.AsNoTracking()
                on salesReturn.IdEstado equals status.IdEstado
            where salesReturn.IdDevolucion == returnId
            select new
            {
                Return = salesReturn,
                ClientName = client.NombreCliente + " " + client.ApellidoCliente,
                Status = status.NombreEstado
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var items = await
        (
            from detail in _dbContext.DevolucionesDetalle.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdDevolucion == returnId
            orderby detail.IdDevolucionDetalle
            select new ManagedReturnItemResponse(
                detail.IdDevolucionDetalle,
                detail.CodProducto,
                product.NombreProducto,
                detail.Cantidad,
                detail.PrecioCosto,
                detail.PrecioVenta,
                detail.MontoLinea ?? detail.PrecioVenta * detail.Cantidad,
                detail.NumeroLote)
        ).ToArrayAsync(cancellationToken);

        return new ManagedReturnResponse(
            header.Return.IdDevolucion,
            header.Return.IdFactura,
            header.Return.NumeroDevolucion,
            header.Return.CodCliente,
            header.ClientName,
            header.Return.CodSucursal,
            header.Return.CodUsuario,
            header.Return.CodTipoPago,
            header.Return.FechaDevolucion,
            header.Return.TotalDevolucion,
            header.Return.CreditoAplicado,
            header.Return.ReembolsoPendiente,
            header.Return.IdEstado,
            header.Status,
            header.Return.Motivo,
            items);
    }

    private static SalesReturnManagementResult Failure(
        SalesReturnManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateReturnNumber()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"DEV-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }

    private static string Limit(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static IReadOnlyDictionary<string, decimal> AllocateNetLineAmounts(
        IEnumerable<FacturaDetalleContable> invoiceDetails,
        IReadOnlySet<string> eligibleProductCodes,
        decimal totalDiscount,
        decimal eligibleGross)
    {
        var details = invoiceDetails
            .OrderBy(item => item.IdFacturaDetalle)
            .ToArray();
        var eligibleDetails = details
            .Where(item => eligibleProductCodes.Contains(item.CodProducto))
            .ToArray();
        var discounts = new Dictionary<string, decimal>(
            StringComparer.OrdinalIgnoreCase);
        var allocatedDiscount = 0m;

        for (var index = 0; index < eligibleDetails.Length; index++)
        {
            var detail = eligibleDetails[index];
            var lineGross = detail.PrecioVenta * detail.Cantidad;
            var lineDiscount = index == eligibleDetails.Length - 1
                ? totalDiscount - allocatedDiscount
                : decimal.Round(
                    totalDiscount * lineGross / eligibleGross,
                    2,
                    MidpointRounding.AwayFromZero);
            lineDiscount = Math.Min(lineDiscount, lineGross);
            discounts[detail.CodProducto] = lineDiscount;
            allocatedDiscount += lineDiscount;
        }

        return details.ToDictionary(
            item => item.CodProducto,
            item => decimal.Round(
                (item.PrecioVenta * item.Cantidad) -
                discounts.GetValueOrDefault(item.CodProducto),
                2,
                MidpointRounding.AwayFromZero),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private sealed record NormalizedReturnItem(
        string ProductCode,
        int Quantity,
        string? LotNumber);
}
