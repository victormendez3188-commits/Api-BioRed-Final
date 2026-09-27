using System.Data;
using BioRed.Application.Accounting;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Accounting;

public sealed class PayableManagementService : IPayableManagementService
{
    private const int RegisteredPurchaseStatusId = 1;
    private const int PendingStatusId = 40;
    private const int PaidStatusId = 41;
    private const int PartialStatusId = 42;
    private const decimal MaximumAmount = 9_999_999_999_999_999.99m;
    private readonly BioRedDbContext _dbContext;

    public PayableManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetPayablesResponse> GetAsync(
        GetPayablesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CuentasPorPagar.AsNoTracking().AsQueryable();

        if (!request.IncludePaid)
        {
            query = query.Where(item => item.SaldoCxp > 0m);
        }

        if (!string.IsNullOrWhiteSpace(request.SupplierCode))
        {
            var supplierCode = NormalizeCode(request.SupplierCode);
            query = query.Where(item => item.CodProveedor == supplierCode);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = NormalizeCode(request.BranchCode);
            query = query.Where(item => item.CodSucursal == branchCode);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await
        (
            from payable in query
            join supplier in _dbContext.Proveedores.AsNoTracking()
                on payable.CodProveedor equals supplier.CodProveedor
            join status in _dbContext.Estados.AsNoTracking()
                on payable.IdEstado equals status.IdEstado
            orderby payable.FechaVencimiento, payable.IdCxp
            select new PayableSummaryResponse(
                payable.IdCxp,
                payable.IdCompra,
                payable.CodProveedor,
                supplier.NombreProveedor,
                payable.CodSucursal,
                payable.TotalCxp,
                payable.SaldoCxp,
                payable.FechaVencimiento,
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

    public async Task<PayableManagementResult> GetByIdAsync(
        int payableId,
        CancellationToken cancellationToken = default)
    {
        var response = await BuildResponseAsync(payableId, cancellationToken);
        return response is null
            ? Failure(PayableManagementStatus.NotFound, $"No existe la cuenta por pagar {payableId}.")
            : new(PayableManagementStatus.Success, response);
    }

    public async Task<PayableManagementResult> CreateFromPurchaseAsync(
        string actorReferenceId,
        CreatePayableFromPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = request.DueDate ?? today;

        if (dueDate < today)
        {
            return Failure(
                PayableManagementStatus.InvalidDueDate,
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

                if (!await UserExistsAsync(actorCode, cancellationToken))
                {
                    return Failure(
                        PayableManagementStatus.UserNotFound,
                        "La cuenta autenticada no está vinculada con un usuario activo.");
                }

                if (!await _dbContext.Estados.AsNoTracking().AnyAsync(
                        item => item.IdEstado == PendingStatusId && item.Activo,
                        cancellationToken))
                {
                    return Failure(
                        PayableManagementStatus.StatusNotConfigured,
                        "No está configurado el estado Pendiente para cuentas por pagar.");
                }

                var purchase = await _dbContext.Compras.AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.IdCompra == request.PurchaseId,
                        cancellationToken);

                if (purchase is null)
                {
                    return Failure(
                        PayableManagementStatus.PurchaseNotFound,
                        $"No existe la compra {request.PurchaseId}.");
                }

                if (purchase.IdEstado != RegisteredPurchaseStatusId)
                {
                    return Failure(
                        PayableManagementStatus.PurchaseCancelled,
                        "Solo una compra Registrada puede generar una cuenta por pagar.");
                }

                if (purchase.TotalCompra <= 0m || purchase.TotalCompra > MaximumAmount)
                {
                    return Failure(
                        PayableManagementStatus.InvalidAmount,
                        "El total de la compra no es válido para generar una cuenta por pagar.");
                }

                if (await _dbContext.CuentasPorPagar.AsNoTracking().AnyAsync(
                        item => item.IdCompra == request.PurchaseId,
                        cancellationToken))
                {
                    return Failure(
                        PayableManagementStatus.DuplicatePurchase,
                        "La compra ya tiene una cuenta por pagar.");
                }

                var payable = new CuentaPorPagar
                {
                    CodProveedor = purchase.CodProveedor,
                    CodUsuario = actorCode,
                    CodSucursal = purchase.CodSucursal,
                    FechaCxp = DateTime.UtcNow,
                    TotalCxp = purchase.TotalCompra,
                    IdEstado = PendingStatusId,
                    IdCompra = purchase.IdCompra,
                    SaldoCxp = purchase.TotalCompra,
                    FechaVencimiento = dueDate
                };

                _dbContext.CuentasPorPagar.Add(payable);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var response = await BuildResponseAsync(payable.IdCxp, cancellationToken);
                if (response is null)
                {
                    return Failure(
                        PayableManagementStatus.PurchaseNotFound,
                        "No fue posible verificar la cuenta por pagar antes de confirmar la transacción.");
                }

                await transaction.CommitAsync(cancellationToken);

                return new PayableManagementResult(
                    PayableManagementStatus.Success,
                    response);
            });
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            return Failure(
                PayableManagementStatus.DuplicatePurchase,
                "La compra ya tiene una cuenta por pagar.");
        }
    }

    public async Task<PayableManagementResult> RegisterPaymentAsync(
        int payableId,
        string actorReferenceId,
        RegisterPayablePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        var paymentTypeCode = NormalizeCode(request.PaymentTypeCode);
        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);

        if (request.Amount != amount)
        {
            return Failure(
                PayableManagementStatus.InvalidAmount,
                "El monto del pago admite como máximo dos decimales.");
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            if (!await UserExistsAsync(actorCode, cancellationToken))
            {
                return Failure(
                    PayableManagementStatus.UserNotFound,
                    "La cuenta autenticada no está vinculada con un usuario activo.");
            }

            if (!await _dbContext.TiposPago.AsNoTracking().AnyAsync(
                    item => item.CodTipoPago == paymentTypeCode && item.Estado,
                    cancellationToken))
            {
                return Failure(
                    PayableManagementStatus.PaymentTypeNotFound,
                    $"No existe el tipo de pago activo {paymentTypeCode}.");
            }

            var statusCount = await _dbContext.Estados.AsNoTracking().CountAsync(
                item =>
                    (item.IdEstado == PaidStatusId || item.IdEstado == PartialStatusId) &&
                    item.Activo,
                cancellationToken);

            if (statusCount != 2)
            {
                return Failure(
                    PayableManagementStatus.StatusNotConfigured,
                    "No están configurados los estados de pago de cuentas por pagar.");
            }

            var payable = await _dbContext.CuentasPorPagar.SingleOrDefaultAsync(
                item => item.IdCxp == payableId,
                cancellationToken);

            if (payable is null)
            {
                return Failure(
                    PayableManagementStatus.NotFound,
                    $"No existe la cuenta por pagar {payableId}.");
            }

            if (payable.SaldoCxp <= 0m)
            {
                return Failure(
                    PayableManagementStatus.AlreadyPaid,
                    "La cuenta por pagar ya está liquidada.");
            }

            if (amount <= 0m || amount > payable.SaldoCxp)
            {
                return Failure(
                    PayableManagementStatus.InvalidAmount,
                    $"El pago debe ser mayor que cero y no superar el saldo {payable.SaldoCxp:F2}.");
            }

            var previousBalance = payable.SaldoCxp;
            var newBalance = decimal.Round(
                previousBalance - amount,
                2,
                MidpointRounding.AwayFromZero);

            payable.SaldoCxp = newBalance;
            payable.IdEstado = newBalance == 0m ? PaidStatusId : PartialStatusId;

            _dbContext.CuentasPorPagarDetalle.Add(new CuentaPorPagarMovimiento
            {
                IdCxp = payable.IdCxp,
                MontoPagado = amount,
                FechaPago = DateTime.UtcNow,
                TipoMovimiento = "Pago",
                SaldoAnterior = previousBalance,
                SaldoNuevo = newBalance,
                CodUsuario = actorCode,
                CodTipoPago = paymentTypeCode,
                Observaciones = CleanOptional(request.Observations),
                ReferenciaExterna = CleanOptional(request.ExternalReference)
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            var response = await BuildResponseAsync(payableId, cancellationToken);
            if (response is null)
            {
                return Failure(
                    PayableManagementStatus.NotFound,
                    "No fue posible verificar el pago antes de confirmar la transacción.");
            }

            await transaction.CommitAsync(cancellationToken);

            return new PayableManagementResult(
                PayableManagementStatus.Success,
                response);
        });
    }

    private async Task<PayableResponse?> BuildResponseAsync(
        int payableId,
        CancellationToken cancellationToken)
    {
        var header = await
        (
            from payable in _dbContext.CuentasPorPagar.AsNoTracking()
            join purchase in _dbContext.Compras.AsNoTracking()
                on payable.IdCompra equals purchase.IdCompra
            join supplier in _dbContext.Proveedores.AsNoTracking()
                on payable.CodProveedor equals supplier.CodProveedor
            join status in _dbContext.Estados.AsNoTracking()
                on payable.IdEstado equals status.IdEstado
            where payable.IdCxp == payableId
            select new
            {
                Payable = payable,
                Purchase = purchase,
                SupplierName = supplier.NombreProveedor,
                Status = status.NombreEstado
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var movements = await _dbContext.CuentasPorPagarDetalle.AsNoTracking()
            .Where(item => item.IdCxp == payableId)
            .OrderBy(item => item.FechaPago)
            .ThenBy(item => item.IdCxpDetalle)
            .Select(item => new AccountMovementResponse(
                item.IdCxpDetalle,
                item.TipoMovimiento,
                item.MontoPagado,
                item.SaldoAnterior,
                item.SaldoNuevo,
                item.CodUsuario,
                item.CodTipoPago,
                item.ReferenciaExterna,
                item.Observaciones,
                item.FechaPago))
            .ToArrayAsync(cancellationToken);

        return new PayableResponse(
            header.Payable.IdCxp,
            header.Payable.IdCompra,
            BuildDocument(
                header.Purchase.SerieDocumento,
                header.Purchase.NumeroDocumento),
            header.Payable.CodProveedor,
            header.SupplierName,
            header.Payable.CodSucursal,
            header.Payable.FechaCxp,
            header.Payable.FechaVencimiento,
            header.Payable.TotalCxp,
            header.Payable.SaldoCxp,
            header.Payable.IdEstado,
            header.Status,
            movements);
    }

    private Task<bool> UserExistsAsync(
        string userCode,
        CancellationToken cancellationToken) =>
        _dbContext.Usuarios.AsNoTracking().AnyAsync(
            item => item.CodUsuario == userCode && item.Estado,
            cancellationToken);

    private static PayableManagementResult Failure(
        PayableManagementStatus status,
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
