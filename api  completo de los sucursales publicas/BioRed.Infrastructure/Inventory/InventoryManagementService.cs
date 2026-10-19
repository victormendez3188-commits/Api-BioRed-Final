using System.Data;
using BioRed.Application.Inventory;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Inventory;

public sealed class InventoryManagementService : IInventoryManagementService
{
    private readonly BioRedDbContext _dbContext;

    public InventoryManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetBranchInventoryResponse?> GetInventoryAsync(
        string branchCode,
        GetBranchInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedBranchCode = branchCode.Trim().ToUpperInvariant();
        var branchExists = await _dbContext.Sucursales
            .AsNoTracking()
            .AnyAsync(
                item => item.CodSucursal == normalizedBranchCode && item.Estado,
                cancellationToken);

        if (!branchExists)
        {
            return null;
        }

        var query =
            from inventory in _dbContext.Inventarios.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on inventory.CodProducto equals product.CodProducto
            where inventory.CodSucursal == normalizedBranchCode
            select new { inventory, product };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.product.CodProducto.Contains(search) ||
                item.product.NombreProducto.Contains(search));
        }

        if (request.OnlyLowStock)
        {
            query = query.Where(item =>
                item.inventory.CantidadActual <= item.inventory.StockMinimo);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.product.NombreProducto)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new InventoryItemResponse(
                item.inventory.IdInventario,
                item.inventory.CodSucursal,
                item.product.CodProducto,
                item.product.NombreProducto,
                item.inventory.CantidadActual,
                item.inventory.StockMinimo,
                item.inventory.StockMaximo,
                item.inventory.CantidadActual <= item.inventory.StockMinimo,
                item.inventory.UltimaActualizacion))
            .ToArrayAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(
            normalizedBranchCode,
            request.Page,
            request.PageSize,
            totalItems,
            totalPages,
            items);
    }

    public async Task<IReadOnlyCollection<ProductLotResponse>?> GetLotsAsync(
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedProductCode = productCode.Trim().ToUpperInvariant();
        var productExists = await _dbContext.Productos
            .AsNoTracking()
            .AnyAsync(
                item => item.CodProducto == normalizedProductCode,
                cancellationToken);

        if (!productExists)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lots = await _dbContext.Lotes
            .AsNoTracking()
            .Where(item => item.CodProducto == normalizedProductCode)
            .OrderBy(item => item.FechaVencimiento)
            .ThenBy(item => item.NumeroLote)
            .ToArrayAsync(cancellationToken);

        return lots
            .Select(item => new ProductLotResponse(
                item.IdLote,
                item.CodProducto,
                item.NumeroLote,
                item.FechaVencimiento,
                item.CantidadLote,
                item.FechaVencimiento < today,
                item.FechaVencimiento.DayNumber - today.DayNumber))
            .ToArray();
    }

    public async Task<InventoryMovementResult> RegisterMovementAsync(
        RegisterInventoryMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        var branchCode = request.BranchCode.Trim().ToUpperInvariant();
        var productCode = request.ProductCode.Trim().ToUpperInvariant();
        var lotNumber = CleanOptional(request.LotNumber)?.ToUpperInvariant();

        if (lotNumber is null && request.ExpirationDate.HasValue)
        {
            return Failure(
                InventoryManagementStatus.InvalidLot,
                "Debe enviar el número de lote junto con la fecha de vencimiento.");
        }

        var branchExists = await _dbContext.Sucursales.AnyAsync(
            item => item.CodSucursal == branchCode && item.Estado,
            cancellationToken);

        if (!branchExists)
        {
            return Failure(
                InventoryManagementStatus.BranchNotFound,
                $"No existe la sucursal activa {branchCode}.");
        }

        var productExists = await _dbContext.Productos.AnyAsync(
            item => item.CodProducto == productCode && item.Estado,
            cancellationToken);

        if (!productExists)
        {
            return Failure(
                InventoryManagementStatus.ProductNotFound,
                $"No existe el producto activo {productCode}.");
        }

        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

        var inventory = await _dbContext.Inventarios.FirstOrDefaultAsync(
            item =>
                item.CodSucursal == branchCode &&
                item.CodProducto == productCode,
            cancellationToken);

        if (inventory is null && request.MovementType == "Salida")
        {
            return Failure(
                InventoryManagementStatus.InventoryNotFound,
                "El producto todavía no tiene existencias registradas en esa sucursal.");
        }

        if (inventory is null)
        {
            inventory = new Inventario
            {
                CodSucursal = branchCode,
                CodProducto = productCode,
                CantidadActual = 0,
                StockMinimo = request.MinimumStock ?? 0,
                StockMaximo = request.MaximumStock ?? 1_000_000,
                UltimaActualizacion = DateTime.UtcNow
            };

            _dbContext.Inventarios.Add(inventory);
        }

        var minimumStock = request.MinimumStock ?? inventory.StockMinimo;
        var maximumStock = request.MaximumStock ?? inventory.StockMaximo;

        if (maximumStock < minimumStock)
        {
            return Failure(
                InventoryManagementStatus.InvalidStockLevels,
                "El stock máximo no puede ser menor que el stock mínimo.");
        }

        var previousQuantity = inventory.CantidadActual;
        var currentQuantity = request.MovementType == "Entrada"
            ? previousQuantity + request.Quantity
            : previousQuantity - request.Quantity;

        if (currentQuantity < 0)
        {
            return Failure(
                InventoryManagementStatus.InsufficientStock,
                $"Solo hay {previousQuantity} unidades disponibles.");
        }

        if (maximumStock > 0 && currentQuantity > maximumStock)
        {
            return Failure(
                InventoryManagementStatus.InvalidStockLevels,
                $"El movimiento supera el stock máximo configurado ({maximumStock}).");
        }

        var lotResult = await UpdateLotAsync(
            productCode,
            lotNumber,
            request,
            cancellationToken);

        if (lotResult.Status != InventoryManagementStatus.Success)
        {
            return Failure(lotResult.Status, lotResult.Detail);
        }

        var registeredAtUtc = DateTime.UtcNow;

        inventory.CantidadActual = currentQuantity;
        inventory.StockMinimo = minimumStock;
        inventory.StockMaximo = maximumStock;
        inventory.UltimaActualizacion = registeredAtUtc;

        var kardex = new KardexMovimiento
        {
            CodProducto = productCode,
            CodSucursal = branchCode,
            TipoMovimiento = request.MovementType,
            Cantidad = request.Quantity,
            FechaMovimiento = registeredAtUtc,
            ReferenciaId = request.ReferenceId,
            Observacion = BuildObservation(request.Observation, lotNumber)
        };

        _dbContext.Kardex.Add(kardex);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

            return new(
                InventoryManagementStatus.Success,
                new InventoryMovementResponse(
                    kardex.IdKardex,
                    branchCode,
                    productCode,
                    request.MovementType,
                    request.Quantity,
                    previousQuantity,
                    currentQuantity,
                    lotNumber,
                    registeredAtUtc));
        });
    }

    public async Task<GetKardexResponse> GetKardexAsync(
        GetKardexRequest request,
        CancellationToken cancellationToken = default)
    {
        var query =
            from movement in _dbContext.Kardex.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on movement.CodProducto equals product.CodProducto
            select new { movement, product };

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = request.BranchCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.movement.CodSucursal == branchCode);
        }

        if (!string.IsNullOrWhiteSpace(request.ProductCode))
        {
            var productCode = request.ProductCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.movement.CodProducto == productCode);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(item =>
                item.movement.FechaMovimiento >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(item =>
                item.movement.FechaMovimiento <= request.ToUtc.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.movement.FechaMovimiento)
            .ThenByDescending(item => item.movement.IdKardex)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new KardexMovementResponse(
                item.movement.IdKardex,
                item.movement.CodProducto,
                item.product.NombreProducto,
                item.movement.CodSucursal,
                item.movement.TipoMovimiento,
                item.movement.Cantidad,
                item.movement.FechaMovimiento,
                item.movement.ReferenciaId,
                item.movement.Observacion))
            .ToArrayAsync(cancellationToken);

        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)request.PageSize);

        return new(
            request.Page,
            request.PageSize,
            totalItems,
            totalPages,
            items);
    }

    private async Task<(InventoryManagementStatus Status, string? Detail)> UpdateLotAsync(
        string productCode,
        string? lotNumber,
        RegisterInventoryMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (lotNumber is null)
        {
            return (InventoryManagementStatus.Success, null);
        }

        var lot = await _dbContext.Lotes.FirstOrDefaultAsync(
            item =>
                item.CodProducto == productCode &&
                item.NumeroLote == lotNumber,
            cancellationToken);

        if (request.MovementType == "Entrada")
        {
            if (lot is null)
            {
                if (!request.ExpirationDate.HasValue)
                {
                    return (
                        InventoryManagementStatus.InvalidLot,
                        "Para crear un lote nuevo debe indicar su fecha de vencimiento.");
                }

                if (request.ExpirationDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
                {
                    return (
                        InventoryManagementStatus.InvalidLot,
                        "No se puede registrar la entrada de un lote vencido.");
                }

                lot = new Lote
                {
                    CodProducto = productCode,
                    NumeroLote = lotNumber,
                    FechaVencimiento = request.ExpirationDate.Value,
                    CantidadLote = request.Quantity
                };

                _dbContext.Lotes.Add(lot);
                return (InventoryManagementStatus.Success, null);
            }

            if (request.ExpirationDate.HasValue &&
                request.ExpirationDate.Value != lot.FechaVencimiento)
            {
                return (
                    InventoryManagementStatus.InvalidLot,
                    "La fecha enviada no coincide con la fecha registrada para ese lote.");
            }

            lot.CantidadLote += request.Quantity;
            return (InventoryManagementStatus.Success, null);
        }

        if (lot is null)
        {
            return (
                InventoryManagementStatus.LotNotFound,
                $"No existe el lote {lotNumber} para el producto {productCode}.");
        }

        if (lot.CantidadLote < request.Quantity)
        {
            return (
                InventoryManagementStatus.InsufficientStock,
                $"El lote {lotNumber} solo tiene {lot.CantidadLote} unidades.");
        }

        lot.CantidadLote -= request.Quantity;
        return (InventoryManagementStatus.Success, null);
    }

    private static InventoryMovementResult Failure(
        InventoryManagementStatus status,
        string? detail) =>
        new(status, Detail: detail);

    private static string? BuildObservation(string? observation, string? lotNumber)
    {
        var cleanObservation = CleanOptional(observation);
        if (lotNumber is null)
        {
            return cleanObservation;
        }

        var result = cleanObservation is null
            ? $"Lote: {lotNumber}."
            : $"{cleanObservation} Lote: {lotNumber}.";

        return result.Length <= 200 ? result : result[..200];
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
