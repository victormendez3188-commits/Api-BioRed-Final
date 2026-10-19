using System.Data;
using BioRed.Application.Accounting;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Accounting;

public sealed class ReceivableManagementService : IReceivableManagementService
{
    private const int PaidStatusId = 31;
    private const int PartialStatusId = 32;
    private readonly BioRedDbContext _dbContext;

    public ReceivableManagementService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetReceivablesResponse> GetAsync(
        GetReceivablesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CuentasPorCobrar.AsNoTracking().AsQueryable();

        if (!request.IncludePaid)
        {
            query = query.Where(item => item.SaldoCxc > 0m);
        }

        if (!string.IsNullOrWhiteSpace(request.ClientCode))
        {
            var clientCode = NormalizeCode(request.ClientCode);
            query = query.Where(item => item.CodCliente == clientCode);
        }

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var branchCode = NormalizeCode(request.BranchCode);
            query = query.Where(item => item.CodSucursal == branchCode);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await
        (
            from receivable in query
            join client in _dbContext.Clientes.AsNoTracking()
                on receivable.CodCliente equals client.CodCliente
            join status in _dbContext.Estados.AsNoTracking()
                on receivable.IdEstado equals status.IdEstado
            orderby receivable.FechaVencimiento, receivable.IdCxc
            select new ReceivableSummaryResponse(
                receivable.IdCxc,
                receivable.IdFactura,
                receivable.CodCliente,
                client.NombreCliente + " " + client.ApellidoCliente,
                receivable.CodSucursal,
                receivable.TotalCxc,
                receivable.SaldoCxc,
                receivable.FechaVencimiento,
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

    public async Task<ReceivableManagementResult> GetByIdAsync(
        int receivableId,
        CancellationToken cancellationToken = default)
    {
        var response = await BuildResponseAsync(receivableId, cancellationToken);
        return response is null
            ? Failure(ReceivableManagementStatus.NotFound, $"No existe la cuenta por cobrar {receivableId}.")
            : new(ReceivableManagementStatus.Success, response);
    }

    public async Task<ReceivableManagementResult> RegisterPaymentAsync(
        int receivableId,
        string actorReferenceId,
        RegisterReceivablePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorCode = NormalizeCode(actorReferenceId);
        var paymentTypeCode = NormalizeCode(request.PaymentTypeCode);
        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);

        if (request.Amount != amount)
        {
            return Failure(
                ReceivableManagementStatus.InvalidAmount,
                "El monto del pago admite como máximo dos decimales.");
        }

        if (paymentTypeCode == "PAGO-PAYPAL")
        {
            return Failure(
                ReceivableManagementStatus.PayPalRequiresVerifiedCapture,
                "Los pagos PayPal solo se registran después de validar una captura real en PayPal Sandbox.");
        }


        if (paymentTypeCode == "PAGO-EFECTIVO")
        {
            return Failure(
                ReceivableManagementStatus.CashRequiresDeliveredOrder,
                "El efectivo contra entrega debe confirmarse desde el endpoint formal de pagos después de entregar el pedido.");
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

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
                    ReceivableManagementStatus.UserNotFound,
                    "La cuenta autenticada no está vinculada con un usuario activo.");
            }

            if (!await _dbContext.TiposPago.AsNoTracking().AnyAsync(
                    item => item.CodTipoPago == paymentTypeCode && item.Estado,
                    cancellationToken))
            {
                return Failure(
                    ReceivableManagementStatus.PaymentTypeNotFound,
                    $"No existe el tipo de pago activo {paymentTypeCode}.");
            }

            var configuredStatuses = await _dbContext.Estados.AsNoTracking()
                .CountAsync(
                    item =>
                        (item.IdEstado == PaidStatusId || item.IdEstado == PartialStatusId) &&
                        item.Activo,
                    cancellationToken);

            if (configuredStatuses != 2)
            {
                return Failure(
                    ReceivableManagementStatus.StatusNotConfigured,
                    "No están configurados los estados de pago de cuentas por cobrar.");
            }

            var receivable = await _dbContext.CuentasPorCobrar.SingleOrDefaultAsync(
                item => item.IdCxc == receivableId,
                cancellationToken);

            if (receivable is null)
            {
                return Failure(
                    ReceivableManagementStatus.NotFound,
                    $"No existe la cuenta por cobrar {receivableId}.");
            }

            if (receivable.SaldoCxc <= 0m)
            {
                return Failure(
                    ReceivableManagementStatus.AlreadyPaid,
                    "La cuenta por cobrar ya está liquidada.");
            }

            if (amount <= 0m || amount > receivable.SaldoCxc)
            {
                return Failure(
                    ReceivableManagementStatus.InvalidAmount,
                    $"El pago debe ser mayor que cero y no superar el saldo {receivable.SaldoCxc:F2}.");
            }

            var previousBalance = receivable.SaldoCxc;
            var newBalance = decimal.Round(
                previousBalance - amount,
                2,
                MidpointRounding.AwayFromZero);

            receivable.SaldoCxc = newBalance;
            receivable.IdEstado = newBalance == 0m ? PaidStatusId : PartialStatusId;

            _dbContext.CuentasPorCobrarDetalle.Add(new CuentaPorCobrarMovimiento
            {
                IdCxc = receivable.IdCxc,
                IdFactura = receivable.IdFactura,
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

            var response = await BuildResponseAsync(receivableId, cancellationToken);
            if (response is null)
            {
                return Failure(
                    ReceivableManagementStatus.NotFound,
                    "No fue posible verificar el cobro antes de confirmar la transacción.");
            }

            await transaction.CommitAsync(cancellationToken);

            return new ReceivableManagementResult(
                ReceivableManagementStatus.Success,
                response);
        });
    }

    private async Task<ReceivableResponse?> BuildResponseAsync(
        int receivableId,
        CancellationToken cancellationToken)
    {
        var header = await
        (
            from receivable in _dbContext.CuentasPorCobrar.AsNoTracking()
            join invoice in _dbContext.Facturas.AsNoTracking()
                on receivable.IdFactura equals invoice.IdFactura
            join client in _dbContext.Clientes.AsNoTracking()
                on receivable.CodCliente equals client.CodCliente
            join status in _dbContext.Estados.AsNoTracking()
                on receivable.IdEstado equals status.IdEstado
            where receivable.IdCxc == receivableId
            select new
            {
                Receivable = receivable,
                Invoice = invoice,
                ClientName = client.NombreCliente + " " + client.ApellidoCliente,
                Status = status.NombreEstado
            }
        ).SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var movements = await _dbContext.CuentasPorCobrarDetalle.AsNoTracking()
            .Where(item => item.IdCxc == receivableId)
            .OrderBy(item => item.FechaPago)
            .ThenBy(item => item.IdCxcDetalle)
            .Select(item => new AccountMovementResponse(
                item.IdCxcDetalle,
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

        return new ReceivableResponse(
            header.Receivable.IdCxc,
            header.Invoice.IdFactura,
            BuildDocument(header.Invoice.SerieDocumento, header.Invoice.NumeroDocumento),
            header.Invoice.IdPedido,
            header.Receivable.CodCliente,
            header.ClientName,
            header.Receivable.CodSucursal,
            header.Receivable.FechaCxc,
            header.Receivable.FechaVencimiento,
            header.Receivable.TotalCxc,
            header.Receivable.SaldoCxc,
            header.Receivable.IdEstado,
            header.Status,
            movements);
    }

    private static ReceivableManagementResult Failure(
        ReceivableManagementStatus status,
        string detail) =>
        new(status, Detail: detail);

    private static string NormalizeCode(string value) =>
        value.Trim().ToUpperInvariant();

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildDocument(string? series, string number) =>
        string.IsNullOrWhiteSpace(series) ? number : $"{series}-{number}";
}
