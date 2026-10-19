using System.Data;
using BioRed.Application.Purchasing;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Purchasing;

public sealed class PurchaseManagementService : IPurchaseManagementService
{
    private const int RegisteredStatusId = 1;
    private const int CancelledStatusId = 2;
    private const decimal MaximumTotal = 9_999_999_999_999_999.99m;
    private readonly BioRedDbContext _dbContext;

    public PurchaseManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetManagedPurchasesResponse> GetAsync(
        GetManagedPurchasesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Compras.AsNoTracking().AsQueryable();

        if (!request.IncludeCancelled)
        {
            query = query.Where(item => item.IdEstado == RegisteredStatusId);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = NormalizeCode(request.BranchCode);
            query = query.Where(item => item.CodSucursal == branchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.SupplierCode))
        {
            var supplierCode = NormalizeCode(request.SupplierCode);
            query = query.Where(item => item.CodProveedor == supplierCode);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(item => item.FechaCompra >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(item => item.FechaCompra <= request.ToUtc.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await
        (
            from purchase in query
            join supplier in _dbContext.Proveedores.AsNoTracking()
                on purchase.CodProveedor equals supplier.CodProveedor
            join paymentType in _dbContext.TiposPago.AsNoTracking()
                on purchase.CodTipoPago equals paymentType.CodTipoPago
            join status in _dbContext.Estados.AsNoTracking()
                on purchase.IdEstado equals status.IdEstado
            orderby purchase.FechaCompra descending, purchase.IdCompra descending
            select new
            {
                Purchase = purchase,
                Supplier = supplier,
                PaymentType = paymentType,
                Status = status
            }
        )
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(row => new ManagedPurchaseSummaryResponse(
                row.Purchase.IdCompra,
                row.Supplier.CodProveedor,
                row.Supplier.NombreProveedor,
                row.Purchase.CodSucursal,
                BuildDocument(
                    row.Purchase.SerieDocumento,
                    row.Purchase.NumeroDocumento),
                row.Purchase.FechaCompra,
                row.Purchase.TotalCompra,
                row.PaymentType.CodTipoPago,
                row.PaymentType.NombreTipoPago,
                row.Status.IdEstado,
                row.Status.NombreEstado))
            .ToArray();

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(request.Page, request.PageSize, totalItems, totalPages, items);
    }

    public async Task<PurchaseManagementResult> GetByIdAsync(
        int purchaseId,
        CancellationToken cancellationToken = default)
    {
        var response = await BuildResponseAsync(purchaseId, cancellationToken);
        return response is null
            ? Failure(
                PurchaseManagementStatus.NotFound,
                $"No existe la compra {purchaseId}.")
            : new(PurchaseManagementStatus.Success, response);
    }

    public async Task<PurchaseManagementResult> CreateAsync(
        string actorReferenceId,
        CreateManagedPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplierCode = NormalizeCode(request.SupplierCode);
        var branchCode = NormalizeCode(request.BranchCode);
        var paymentTypeCode = NormalizeCode(request.PaymentTypeCode);
        var actorCode = NormalizeCode(actorReferenceId);
        var documentSeries = CleanOptional(request.DocumentSeries)?.ToUpperInvariant();
        var documentNumber = request.DocumentNumber.Trim().ToUpperInvariant();
        var purchaseDateUtc = NormalizeUtc(request.PurchaseDateUtc ?? DateTime.UtcNow);
        var items = request.Items
            .Select(item => new NormalizedPurchaseItem(
                NormalizeCode(item.ProductCode),
                item.Quantity,
                item.CostPrice,
                item.SalePrice,
                CleanOptional(item.LotNumber)?.ToUpperInvariant(),
                item.ExpirationDate))
            .ToArray();

        var validation = ValidateCreateRequest(items, purchaseDateUtc);
        if (validation is not null)
        {
            return validation;
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        try
        {
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                var supplier = await _dbContext.Proveedores
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.CodProveedor == supplierCode && item.Estado,
                        cancellationToken);

                if (supplier is null)
                {
                    return Failure(
                        PurchaseManagementStatus.SupplierNotFound,
                        $"No existe el proveedor activo {supplierCode}.");
                }

                var branch = await _dbContext.Sucursales
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.CodSucursal == branchCode && item.Estado,
                        cancellationToken);

                if (branch is null)
                {
                    return Failure(
                        PurchaseManagementStatus.BranchNotFound,
                        $"No existe la sucursal activa {branchCode}.");
                }

                if (!supplier.CodEmpresa.Equals(
                        branch.CodEmpresa,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(
                        PurchaseManagementStatus.SupplierCompanyMismatch,
                        "El proveedor y la sucursal deben pertenecer a la misma empresa.");
                }

                var userExists = await _dbContext.Usuarios
                    .AsNoTracking()
                    .AnyAsync(
                        item => item.CodUsuario == actorCode && item.Estado,
                        cancellationToken);

                if (!userExists)
                {
                    return Failure(
                        PurchaseManagementStatus.UserNotFound,
                        "La cuenta autenticada no está vinculada con un usuario activo.");
                }

                var paymentTypeExists = await _dbContext.TiposPago
                    .AsNoTracking()
                    .AnyAsync(
                        item => item.CodTipoPago == paymentTypeCode && item.Estado,
                        cancellationToken);

                if (!paymentTypeExists)
                {
                    return Failure(
                        PurchaseManagementStatus.PaymentTypeNotFound,
                        $"No existe el tipo de pago activo {paymentTypeCode}.");
                }

                var statusExists = await _dbContext.Estados
                    .AsNoTracking()
                    .AnyAsync(
                        item => item.IdEstado == RegisteredStatusId && item.Activo,
                        cancellationToken);

                if (!statusExists)
                {
                    return Failure(
                        PurchaseManagementStatus.StatusNotConfigured,
                        "No está configurado el estado Registrada para las compras.");
                }

                var duplicateDocument = await _dbContext.Compras
                    .AsNoTracking()
                    .AnyAsync(
                        item => item.CodProveedor == supplierCode &&
                                item.SerieDocumento == documentSeries &&
                                item.NumeroDocumento == documentNumber,
                        cancellationToken);

                if (duplicateDocument)
                {
                    return Failure(
                        PurchaseManagementStatus.DuplicateDocument,
                        "Ya existe una compra con ese proveedor, serie y número de documento.");
                }

                var productCodes = items.Select(item => item.ProductCode).ToArray();
                var products = await _dbContext.Productos
                    .Where(item => productCodes.Contains(item.CodProducto) && item.Estado)
                    .ToArrayAsync(cancellationToken);
                var productsByCode = products.ToDictionary(
                    item => item.CodProducto,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    if (!productsByCode.TryGetValue(item.ProductCode, out var product))
                    {
                        return Failure(
                            PurchaseManagementStatus.ProductNotFound,
                            $"No existe el producto activo {item.ProductCode}.");
                    }

                    if (!product.CodEmpresa.Equals(
                            branch.CodEmpresa,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return Failure(
                            PurchaseManagementStatus.ProductCompanyMismatch,
                            $"El producto {item.ProductCode} no pertenece a la empresa de la sucursal.");
                    }
                }

                var inventories = await _dbContext.Inventarios
                    .Where(item =>
                        item.CodSucursal == branchCode &&
                        productCodes.Contains(item.CodProducto))
                    .ToArrayAsync(cancellationToken);
                var inventoriesByProduct = inventories.ToDictionary(
                    item => item.CodProducto,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    if (inventoriesByProduct.TryGetValue(item.ProductCode, out var inventory) &&
                        inventory.StockMaximo > 0 &&
                        inventory.CantidadActual + item.Quantity > inventory.StockMaximo)
                    {
                        return Failure(
                            PurchaseManagementStatus.InventoryLimitExceeded,
                            $"La compra supera el stock máximo de {item.ProductCode} ({inventory.StockMaximo}).");
                    }
                }

                var lotNumbers = items
                    .Where(item => item.LotNumber is not null)
                    .Select(item => item.LotNumber!)
                    .ToArray();
                var lots = lotNumbers.Length == 0
                    ? Array.Empty<Lote>()
                    : await _dbContext.Lotes
                        .Where(item =>
                            productCodes.Contains(item.CodProducto) &&
                            lotNumbers.Contains(item.NumeroLote))
                        .ToArrayAsync(cancellationToken);

                foreach (var item in items.Where(item => item.LotNumber is not null))
                {
                    var existingLot = lots.SingleOrDefault(lot =>
                        lot.CodProducto == item.ProductCode &&
                        lot.NumeroLote == item.LotNumber);

                    if (existingLot is not null &&
                        existingLot.FechaVencimiento != item.ExpirationDate)
                    {
                        return Failure(
                            PurchaseManagementStatus.InvalidLot,
                            $"La fecha no coincide con el lote existente {item.LotNumber}.");
                    }
                }

                var total = items.Sum(item => item.CostPrice * item.Quantity);
                total = decimal.Round(total, 2, MidpointRounding.AwayFromZero);

                if (total <= 0m || total > MaximumTotal)
                {
                    return Failure(
                        PurchaseManagementStatus.InvalidTotal,
                        "El total calculado no cabe en el formato monetario permitido.");
                }

                var purchase = new CompraEncabezado
                {
                    CodProveedor = supplierCode,
                    CodUsuario = actorCode,
                    CodSucursal = branchCode,
                    FechaCompra = purchaseDateUtc,
                    TotalCompra = total,
                    CodTipoPago = paymentTypeCode,
                    IdEstado = RegisteredStatusId,
                    SerieDocumento = documentSeries,
                    NumeroDocumento = documentNumber,
                    Observaciones = CleanOptional(request.Observations)
                };

                foreach (var item in items)
                {
                    purchase.Detalles.Add(new CompraDetalle
                    {
                        CodProducto = item.ProductCode,
                        Cantidad = item.Quantity,
                        PrecioCosto = item.CostPrice,
                        PrecioVenta = item.SalePrice,
                        NumeroLote = item.LotNumber,
                        FechaVencimiento = item.ExpirationDate
                    });

                    var product = productsByCode[item.ProductCode];
                    product.PrecioCosto = item.CostPrice;
                    product.PrecioVenta = item.SalePrice;

                    if (!inventoriesByProduct.TryGetValue(item.ProductCode, out var inventory))
                    {
                        inventory = new Inventario
                        {
                            CodSucursal = branchCode,
                            CodProducto = item.ProductCode,
                            CantidadActual = 0,
                            StockMinimo = 0,
                            StockMaximo = 1_000_000,
                            UltimaActualizacion = purchaseDateUtc
                        };

                        inventoriesByProduct.Add(item.ProductCode, inventory);
                        _dbContext.Inventarios.Add(inventory);
                    }

                    inventory.CantidadActual += item.Quantity;
                    inventory.UltimaActualizacion = purchaseDateUtc;

                    if (item.LotNumber is not null)
                    {
                        var lot = lots.SingleOrDefault(existing =>
                            existing.CodProducto == item.ProductCode &&
                            existing.NumeroLote == item.LotNumber);

                        if (lot is null)
                        {
                            lot = new Lote
                            {
                                CodProducto = item.ProductCode,
                                NumeroLote = item.LotNumber,
                                FechaVencimiento = item.ExpirationDate!.Value,
                                CantidadLote = 0
                            };
                            _dbContext.Lotes.Add(lot);
                        }

                        lot.CantidadLote += item.Quantity;
                    }
                }

                _dbContext.Compras.Add(purchase);
                await _dbContext.SaveChangesAsync(cancellationToken);

                foreach (var item in items)
                {
                    _dbContext.Kardex.Add(new KardexMovimiento
                    {
                        CodProducto = item.ProductCode,
                        CodSucursal = branchCode,
                        TipoMovimiento = "Entrada",
                        Cantidad = item.Quantity,
                        FechaMovimiento = purchaseDateUtc,
                        ReferenciaId = purchase.IdCompra,
                        Observacion = BuildPurchaseObservation(
                            purchase.IdCompra,
                            documentSeries,
                            documentNumber,
                            item.LotNumber)
                    });
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var response = await BuildResponseAsync(
                    purchase.IdCompra,
                    cancellationToken);

                return new PurchaseManagementResult(
                    PurchaseManagementStatus.Success,
                    response);
            });
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                PurchaseManagementStatus.DuplicateDocument,
                "El documento o uno de los registros de la compra ya existe.");
        }
    }

    public async Task<PurchaseManagementResult> CancelAsync(
        int purchaseId,
        string actorReferenceId,
        CancelManagedPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        var reason = request.Reason.Trim();
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var userExists = await _dbContext.Usuarios
                .AsNoTracking()
                .AnyAsync(
                    item => item.CodUsuario == actorCode && item.Estado,
                    cancellationToken);

            if (!userExists)
            {
                return Failure(
                    PurchaseManagementStatus.UserNotFound,
                    "La cuenta autenticada no está vinculada con un usuario activo.");
            }

            var cancelledStatusExists = await _dbContext.Estados
                .AsNoTracking()
                .AnyAsync(
                    item => item.IdEstado == CancelledStatusId && item.Activo,
                    cancellationToken);

            if (!cancelledStatusExists)
            {
                return Failure(
                    PurchaseManagementStatus.StatusNotConfigured,
                    "No está configurado el estado Anulada para las compras.");
            }

            var purchase = await _dbContext.Compras
                .Include(item => item.Detalles)
                .SingleOrDefaultAsync(
                    item => item.IdCompra == purchaseId,
                    cancellationToken);

            if (purchase is null)
            {
                return Failure(
                    PurchaseManagementStatus.NotFound,
                    $"No existe la compra {purchaseId}.");
            }

            if (purchase.IdEstado == CancelledStatusId)
            {
                return Failure(
                    PurchaseManagementStatus.AlreadyCancelled,
                    "La compra ya está anulada.");
            }

            var productCodes = purchase.Detalles
                .Select(item => item.CodProducto)
                .ToArray();
            var inventories = await _dbContext.Inventarios
                .Where(item =>
                    item.CodSucursal == purchase.CodSucursal &&
                    productCodes.Contains(item.CodProducto))
                .ToArrayAsync(cancellationToken);
            var inventoryByProduct = inventories.ToDictionary(
                item => item.CodProducto,
                StringComparer.OrdinalIgnoreCase);

            foreach (var detail in purchase.Detalles)
            {
                if (!inventoryByProduct.TryGetValue(detail.CodProducto, out var inventory) ||
                    inventory.CantidadActual < detail.Cantidad)
                {
                    return Failure(
                        PurchaseManagementStatus.InsufficientStockToCancel,
                        $"No hay existencias suficientes de {detail.CodProducto} para anular la compra.");
                }
            }

            var lotNumbers = purchase.Detalles
                .Where(item => item.NumeroLote is not null)
                .Select(item => item.NumeroLote!)
                .ToArray();
            var lots = lotNumbers.Length == 0
                ? Array.Empty<Lote>()
                : await _dbContext.Lotes
                    .Where(item =>
                        productCodes.Contains(item.CodProducto) &&
                        lotNumbers.Contains(item.NumeroLote))
                    .ToArrayAsync(cancellationToken);

            foreach (var detail in purchase.Detalles.Where(item => item.NumeroLote is not null))
            {
                var lot = lots.SingleOrDefault(item =>
                    item.CodProducto == detail.CodProducto &&
                    item.NumeroLote == detail.NumeroLote);

                if (lot is null || lot.CantidadLote < detail.Cantidad)
                {
                    return Failure(
                        PurchaseManagementStatus.InsufficientStockToCancel,
                        $"El lote {detail.NumeroLote} no tiene existencias suficientes para anular.");
                }
            }

            var cancelledAtUtc = DateTime.UtcNow;
            foreach (var detail in purchase.Detalles)
            {
                inventoryByProduct[detail.CodProducto].CantidadActual -= detail.Cantidad;
                inventoryByProduct[detail.CodProducto].UltimaActualizacion = cancelledAtUtc;

                if (detail.NumeroLote is not null)
                {
                    var lot = lots.Single(item =>
                        item.CodProducto == detail.CodProducto &&
                        item.NumeroLote == detail.NumeroLote);
                    lot.CantidadLote -= detail.Cantidad;
                }

                _dbContext.Kardex.Add(new KardexMovimiento
                {
                    CodProducto = detail.CodProducto,
                    CodSucursal = purchase.CodSucursal,
                    TipoMovimiento = "AnulacionEntrada",
                    Cantidad = detail.Cantidad,
                    FechaMovimiento = cancelledAtUtc,
                    ReferenciaId = purchase.IdCompra,
                    Observacion = Limit(
                        $"Compra {purchase.IdCompra} anulada por {actorCode}. Motivo: {reason}",
                        200)
                });
            }

            purchase.IdEstado = CancelledStatusId;
            purchase.Observaciones = Limit(
                string.IsNullOrWhiteSpace(purchase.Observaciones)
                    ? $"ANULADA: {reason}"
                    : $"{purchase.Observaciones} | ANULADA: {reason}",
                300);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var response = await BuildResponseAsync(purchaseId, cancellationToken);
            return new PurchaseManagementResult(
                PurchaseManagementStatus.Success,
                response);
        });
    }

    private async Task<ManagedPurchaseResponse?> BuildResponseAsync(
        int purchaseId,
        CancellationToken cancellationToken)
    {
        var header = await
        (
            from purchase in _dbContext.Compras.AsNoTracking()
            join supplier in _dbContext.Proveedores.AsNoTracking()
                on purchase.CodProveedor equals supplier.CodProveedor
            join branch in _dbContext.Sucursales.AsNoTracking()
                on purchase.CodSucursal equals branch.CodSucursal
            join paymentType in _dbContext.TiposPago.AsNoTracking()
                on purchase.CodTipoPago equals paymentType.CodTipoPago
            join status in _dbContext.Estados.AsNoTracking()
                on purchase.IdEstado equals status.IdEstado
            where purchase.IdCompra == purchaseId
            select new
            {
                Purchase = purchase,
                Supplier = supplier,
                Branch = branch,
                PaymentType = paymentType,
                Status = status
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var items = await
        (
            from detail in _dbContext.ComprasDetalle.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdCompra == purchaseId
            orderby detail.IdCompraDetalle
            select new ManagedPurchaseItemResponse(
                detail.IdCompraDetalle,
                detail.CodProducto,
                product.NombreProducto,
                detail.Cantidad,
                detail.PrecioCosto,
                detail.PrecioVenta,
                detail.PrecioCosto * detail.Cantidad,
                detail.NumeroLote,
                detail.FechaVencimiento)
        ).ToArrayAsync(cancellationToken);

        return new ManagedPurchaseResponse(
            header.Purchase.IdCompra,
            header.Supplier.CodProveedor,
            header.Supplier.NombreProveedor,
            header.Supplier.CodEmpresa,
            header.Branch.CodSucursal,
            header.Branch.NombreSucursal,
            header.Purchase.CodUsuario,
            header.Purchase.SerieDocumento ?? string.Empty,
            header.Purchase.NumeroDocumento,
            header.Purchase.FechaCompra,
            header.Purchase.TotalCompra,
            header.PaymentType.CodTipoPago,
            header.PaymentType.NombreTipoPago,
            header.Status.IdEstado,
            header.Status.NombreEstado,
            header.Purchase.Observaciones,
            items);
    }

    private static PurchaseManagementResult? ValidateCreateRequest(
        IReadOnlyCollection<NormalizedPurchaseItem> items,
        DateTime purchaseDateUtc)
    {
        if (purchaseDateUtc > DateTime.UtcNow.AddMinutes(5))
        {
            return Failure(
                PurchaseManagementStatus.InvalidPurchaseDate,
                "La fecha de compra no puede estar en el futuro.");
        }

        if (items.GroupBy(item => item.ProductCode).Any(group => group.Count() > 1))
        {
            return Failure(
                PurchaseManagementStatus.DuplicateProduct,
                "No puede repetir un producto dentro de la misma compra.");
        }

        foreach (var item in items)
        {
            if (item.CostPrice <= 0m ||
                item.SalePrice <= 0m ||
                item.CostPrice > MaximumTotal ||
                item.SalePrice > MaximumTotal ||
                item.SalePrice < item.CostPrice)
            {
                return Failure(
                    PurchaseManagementStatus.InvalidPrice,
                    $"Revise los precios del producto {item.ProductCode}; la venta no puede ser menor que el costo.");
            }

            var hasLotNumber = item.LotNumber is not null;
            var hasExpirationDate = item.ExpirationDate.HasValue;
            if (hasLotNumber != hasExpirationDate)
            {
                return Failure(
                    PurchaseManagementStatus.InvalidLot,
                    $"Debe enviar lote y vencimiento juntos para {item.ProductCode}.");
            }

            if (item.ExpirationDate.HasValue &&
                item.ExpirationDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return Failure(
                    PurchaseManagementStatus.InvalidLot,
                    $"El lote de {item.ProductCode} debe tener una fecha futura.");
            }
        }

        return null;
    }

    private static PurchaseManagementResult Failure(
        PurchaseManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static string BuildDocument(string? series, string number) =>
        string.IsNullOrWhiteSpace(series) ? number : $"{series}-{number}";

    private static string BuildPurchaseObservation(
        int purchaseId,
        string? series,
        string number,
        string? lotNumber)
    {
        var lot = lotNumber is null ? string.Empty : $" Lote: {lotNumber}.";
        return Limit(
            $"Entrada por compra {purchaseId}, documento {BuildDocument(series, number)}.{lot}",
            200);
    }

    private static string Limit(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        sqlException.Number is 2601 or 2627;

    private sealed record NormalizedPurchaseItem(
        string ProductCode,
        int Quantity,
        decimal CostPrice,
        decimal SalePrice,
        string? LotNumber,
        DateOnly? ExpirationDate);
}
