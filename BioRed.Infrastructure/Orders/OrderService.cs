using System.Data;
using BioRed.Application.Orders;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BioRed.Infrastructure.Orders;

public sealed class OrderService : IOrderService
{
    private const double EarthRadiusKm = 6371.0088;
    private const decimal IncludedDeliveryKm = 3m;
    private const decimal PlatformCommissionRate = 0.10m;

    private readonly BioRedDbContext _dbContext;

    public OrderService(BioRedDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetOrdersResult> GetAsync(
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        bool canViewManagedOrders,
        GetOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorizedQuery = await CreateAuthorizedOrderQueryAsync(
            actorType,
            actorReferenceId,
            isAdministrator,
            canViewManagedOrders,
            cancellationToken);

        if (authorizedQuery is null)
        {
            return new GetOrdersResult(
                OrderQueryStatus.Forbidden,
                Detail: "La cuenta no puede consultar pedidos.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            authorizedQuery = authorizedQuery.Where(
                item => item.EstadoPedido == status);
        }

        var totalItems = await authorizedQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling((double)totalItems / request.PageSize);
        var rowsToSkip = (request.Page - 1) * request.PageSize;

        var items = await
        (
            from order in authorizedQuery
            join client in _dbContext.Clientes.AsNoTracking()
                on order.CodCliente equals client.CodCliente
            join branch in _dbContext.Sucursales.AsNoTracking()
                on order.CodSucursal equals branch.CodSucursal
            orderby order.FechaCreacion descending, order.IdPedido descending
            select new OrderSummaryResponse(
                order.IdPedido,
                order.CodPedido,
                order.CodCliente,
                client.NombreCliente + " " + client.ApellidoCliente,
                order.CodSucursal,
                branch.NombreSucursal,
                order.CodRepartidor,
                order.EstadoPedido,
                order.Subtotal,
                order.CostoEnvio,
                order.TotalCobrado,
                order.FechaCreacion,
                order.FechaEntrega)
        )
        .Skip(rowsToSkip)
        .Take(request.PageSize)
        .ToListAsync(cancellationToken);

        return new GetOrdersResult(
            OrderQueryStatus.Success,
            new GetOrdersResponse(
                request.Page,
                request.PageSize,
                totalItems,
                totalPages,
                items));
    }

    public async Task<GetOrderDetailResult> GetByIdAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        bool canViewManagedOrders,
        CancellationToken cancellationToken = default)
    {
        var authorizedQuery = await CreateAuthorizedOrderQueryAsync(
            actorType,
            actorReferenceId,
            isAdministrator,
            canViewManagedOrders,
            cancellationToken);

        if (authorizedQuery is null)
        {
            return new GetOrderDetailResult(
                OrderQueryStatus.Forbidden,
                Detail: "La cuenta no puede consultar pedidos.");
        }

        var header = await
        (
            from order in authorizedQuery
            join client in _dbContext.Clientes.AsNoTracking()
                on order.CodCliente equals client.CodCliente
            join branch in _dbContext.Sucursales.AsNoTracking()
                on order.CodSucursal equals branch.CodSucursal
            join address in _dbContext.DireccionesCliente.AsNoTracking()
                on order.IdDireccionEntrega equals address.IdDireccion
            join paymentType in _dbContext.TiposPago.AsNoTracking()
                on order.CodTipoPago equals paymentType.CodTipoPago
            where order.IdPedido == orderId
            select new
            {
                order.IdPedido,
                order.CodPedido,
                order.CodCliente,
                ClientName = client.NombreCliente + " " + client.ApellidoCliente,
                order.CodSucursal,
                BranchName = branch.NombreSucursal,
                order.CodRepartidor,
                order.IdDireccionEntrega,
                DeliveryAddressName = address.NombreDireccion,
                DeliveryAddress = address.DireccionCompleta,
                order.CodTipoPago,
                order.CodPromocion,
                PaymentTypeName = paymentType.NombreTipoPago,
                order.EstadoPedido,
                order.Subtotal,
                order.CostoEnvio,
                order.DescuentoAplicado,
                order.TotalCobrado,
                order.NotasCliente,
                order.FechaCreacion,
                order.FechaEntrega
            }
        )
        .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return new GetOrderDetailResult(
                OrderQueryStatus.NotFound,
                Detail: "No se encontró el pedido solicitado.");
        }

        var items = await
        (
            from detail in _dbContext.PedidosDetalle.AsNoTracking()
            join product in _dbContext.Productos.AsNoTracking()
                on detail.CodProducto equals product.CodProducto
            where detail.IdPedido == orderId
            orderby detail.IdPedidoDetalle
            select new OrderDetailItemResponse(
                detail.CodProducto,
                product.NombreProducto,
                detail.Cantidad,
                detail.PrecioUnitario,
                detail.Subtotal,
                detail.RequiereReceta ?? false)
        )
        .ToListAsync(cancellationToken);

        var history = await _dbContext.SeguimientosPedido
            .AsNoTracking()
            .Where(item => item.IdPedido == orderId)
            .OrderBy(item => item.FechaHora)
            .ThenBy(item => item.IdSeguimiento)
            .Select(item => new OrderHistoryResponse(
                item.EstadoPedido,
                item.Descripcion,
                item.ActualizadoPor,
                item.FechaHora))
            .ToListAsync(cancellationToken);

        var lastLocation = await _dbContext.UbicacionesRepartidor
            .AsNoTracking()
            .Where(item => item.IdPedido == orderId)
            .OrderByDescending(item => item.FechaRegistro)
            .ThenByDescending(item => item.IdUbicacion)
            .Select(item => new OrderLocationResponse(
                item.IdUbicacion,
                item.Latitud,
                item.Longitud,
                item.FechaRegistro))
            .FirstOrDefaultAsync(cancellationToken);

        var prescriptionId = await _dbContext.RecetasClientes
            .AsNoTracking()
            .Where(item => item.IdPedido == orderId)
            .Select(item => (long?)item.IdReceta)
            .SingleOrDefaultAsync(cancellationToken);

        return new GetOrderDetailResult(
            OrderQueryStatus.Success,
            new OrderDetailResponse(
                header.IdPedido,
                header.CodPedido,
                header.CodCliente,
                header.ClientName,
                header.CodSucursal,
                header.BranchName,
                header.CodRepartidor,
                header.IdDireccionEntrega,
                header.DeliveryAddressName,
                header.DeliveryAddress,
                header.CodTipoPago,
                header.PaymentTypeName,
                header.EstadoPedido,
                header.Subtotal,
                header.CostoEnvio,
                header.DescuentoAplicado ?? 0m,
                header.TotalCobrado,
                header.NotasCliente,
                header.FechaCreacion,
                header.FechaEntrega,
                items,
                history,
                lastLocation,
                header.CodPromocion,
                prescriptionId));
    }

    public async Task<CreateOrderResult> CreateAsync(
        string clientCode,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(clientCode) ||
            string.IsNullOrWhiteSpace(request.BranchCode) ||
            string.IsNullOrWhiteSpace(request.PaymentTypeCode) ||
            request.DeliveryAddressId <= 0 ||
            request.Items.Count == 0 ||
            request.Items.Any(item =>
                item is null ||
                string.IsNullOrWhiteSpace(item.ProductCode) ||
                item.Quantity <= 0))
        {
            return Failure(
                CreateOrderStatus.InvalidRequest,
                "Verifique la sucursal, dirección, tipo de pago y productos.");
        }

        clientCode = clientCode.Trim().ToUpperInvariant();

        var requestedItems = request.Items
            .GroupBy(
                item => item.ProductCode.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                ProductCode = group.Key.ToUpperInvariant(),
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToArray();

        if (requestedItems.Any(item => item.Quantity > 1000))
        {
            return Failure(
                CreateOrderStatus.InvalidRequest,
                "La cantidad máxima permitida por producto es 1000.");
        }

        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var clientExists = await _dbContext.Clientes
                .AsNoTracking()
                .AnyAsync(
                    item => item.CodCliente == clientCode && item.Estado,
                    cancellationToken);

            if (!clientExists)
            {
                return Failure(
                    CreateOrderStatus.ClientNotFound,
                    "No existe un cliente activo asociado a la cuenta.");
            }

            var branchCode = request.BranchCode.Trim().ToUpperInvariant();
            var branch = await _dbContext.Sucursales
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CodSucursal == branchCode && item.Estado,
                    cancellationToken);

            var configuration = await _dbContext.ConfiguracionesEntrega
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CodSucursal == branchCode && item.Activo,
                    cancellationToken);

            if (branch is null || configuration is null ||
                !branch.Latitud.HasValue || !branch.Longitud.HasValue)
            {
                return Failure(
                    CreateOrderStatus.BranchNotFound,
                    "La sucursal no existe o no tiene entregas configuradas.");
            }

            var address = await _dbContext.DireccionesCliente
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item =>
                        item.IdDireccion == request.DeliveryAddressId &&
                        item.CodCliente == clientCode &&
                        item.Estado,
                    cancellationToken);

            if (address is null)
            {
                return Failure(
                    CreateOrderStatus.AddressNotFound,
                    "La dirección no pertenece al cliente o está inactiva.");
            }

            var paymentTypeCode = request.PaymentTypeCode.Trim().ToUpperInvariant();
            var paymentTypeExists = await _dbContext.TiposPago
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.CodTipoPago == paymentTypeCode &&
                        item.Estado,
                    cancellationToken);

            if (!paymentTypeExists)
            {
                return Failure(
                    CreateOrderStatus.PaymentTypeNotFound,
                    "El tipo de pago no existe o está inactivo.");
            }

            var distanceKm = CalculateDistanceKm(
                branch.Latitud.Value,
                branch.Longitud.Value,
                address.Latitud,
                address.Longitud);

            if (distanceKm > (double)configuration.RadioMaximoKm)
            {
                return Failure(
                    CreateOrderStatus.OutOfDeliveryArea,
                    $"La dirección está a {distanceKm:F2} km y supera el radio de entrega.");
            }

            var productCodes = requestedItems
                .Select(item => item.ProductCode)
                .ToArray();

            var products = await _dbContext.Productos
                .AsNoTracking()
                .Where(item =>
                    productCodes.Contains(item.CodProducto) &&
                    item.CodEmpresa == branch.CodEmpresa &&
                    item.Estado)
                .ToListAsync(cancellationToken);

            var productByCode = products.ToDictionary(
                item => item.CodProducto,
                StringComparer.OrdinalIgnoreCase);

            foreach (var requestedItem in requestedItems)
            {
                if (!productByCode.ContainsKey(requestedItem.ProductCode))
                {
                    return Failure(
                        CreateOrderStatus.ProductNotFound,
                        $"El producto {requestedItem.ProductCode} no está disponible para esta farmacia.");
                }
            }

            RecetaCliente? prescription = null;
            var prescriptionItems = requestedItems
                .Where(item => productByCode[item.ProductCode].RequiereReceta)
                .ToArray();

            if (prescriptionItems.Length > 0)
            {
                if (!request.PrescriptionId.HasValue)
                {
                    return Failure(
                        CreateOrderStatus.PrescriptionRequired,
                        "El pedido contiene productos que requieren una receta aprobada.");
                }

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                prescription = await _dbContext.RecetasClientes
                    .SingleOrDefaultAsync(
                        item => item.IdReceta == request.PrescriptionId.Value,
                        cancellationToken);

                if (prescription is null ||
                    prescription.CodCliente != clientCode ||
                    prescription.Estado != "APPROVED" ||
                    prescription.IdPedido.HasValue ||
                    prescription.FechaEmision > today ||
                    prescription.FechaVencimiento < today)
                {
                    return Failure(
                        CreateOrderStatus.PrescriptionInvalid,
                        "La receta no existe, no pertenece al cliente, no está aprobada, está vencida o ya fue utilizada.");
                }

                var authorizedProducts = await _dbContext.RecetasProductos
                    .AsNoTracking()
                    .Where(item => item.IdReceta == prescription.IdReceta)
                    .ToDictionaryAsync(
                        item => item.CodProducto,
                        item => item.CantidadAutorizada,
                        cancellationToken);

                if (prescriptionItems.Any(item =>
                        !authorizedProducts.TryGetValue(
                            item.ProductCode,
                            out var authorizedQuantity) ||
                        authorizedQuantity < item.Quantity))
                {
                    return Failure(
                        CreateOrderStatus.PrescriptionInvalid,
                        "La receta no autoriza todos los productos o cantidades solicitadas.");
                }
            }
            else if (request.PrescriptionId.HasValue)
            {
                return Failure(
                    CreateOrderStatus.PrescriptionInvalid,
                    "No debe asociar una receta si el pedido no contiene productos regulados.");
            }

            var inventories = await _dbContext.Inventarios
                .Where(item =>
                    item.CodSucursal == branchCode &&
                    productCodes.Contains(item.CodProducto))
                .ToListAsync(cancellationToken);

            foreach (var requestedItem in requestedItems)
            {
                var inventory = inventories.FirstOrDefault(item =>
                    item.CodProducto.Equals(
                        requestedItem.ProductCode,
                        StringComparison.OrdinalIgnoreCase));

                if (inventory is null ||
                    inventory.CantidadActual < requestedItem.Quantity)
                {
                    return Failure(
                        CreateOrderStatus.InsufficientStock,
                        $"No hay existencias suficientes de {requestedItem.ProductCode}.");
                }
            }

            var nowUtc = DateTime.UtcNow;
            var responseItems = requestedItems
                .Select(item =>
                {
                    var product = productByCode[item.ProductCode];
                    var itemSubtotal =
                        decimal.Round(
                            product.PrecioVenta * item.Quantity,
                            2,
                            MidpointRounding.AwayFromZero);

                    return new OrderItemResponse(
                        product.CodProducto,
                        product.NombreProducto,
                        item.Quantity,
                        product.PrecioVenta,
                        itemSubtotal);
                })
                .ToArray();

            var subtotal = responseItems.Sum(item => item.Subtotal);
            Promocion? promotion = null;
            string? promotionCode = null;
            decimal discountApplied = 0m;

            if (!string.IsNullOrWhiteSpace(request.PromotionCode))
            {
                promotionCode = request.PromotionCode.Trim().ToUpperInvariant();
                promotion = await _dbContext.Promociones
                    .SingleOrDefaultAsync(
                        item => item.CodPromocion == promotionCode,
                        cancellationToken);

                if (promotion is null)
                {
                    return Failure(
                        CreateOrderStatus.PromotionNotFound,
                        $"No existe la promoción {promotionCode}.");
                }

                if (!promotion.Activa ||
                    promotion.TipoDescuento is not ("PERCENTAGE" or "FIXED") ||
                    promotion.CodEmpresa != branch.CodEmpresa ||
                    (promotion.CodSucursal is not null &&
                     promotion.CodSucursal != branchCode) ||
                    promotion.InicioUtc > nowUtc ||
                    promotion.FinUtc < nowUtc ||
                    subtotal < promotion.MontoMinimoPedido)
                {
                    return Failure(
                        CreateOrderStatus.PromotionNotApplicable,
                        "La promoción está inactiva, fuera de vigencia o no cumple la sucursal y monto mínimo.");
                }

                var totalUsageCount = await _dbContext.PromocionesUsos
                    .AsNoTracking()
                    .CountAsync(
                        item => item.CodPromocion == promotionCode,
                        cancellationToken);
                var clientUsageCount = await _dbContext.PromocionesUsos
                    .AsNoTracking()
                    .CountAsync(
                        item => item.CodPromocion == promotionCode &&
                                item.CodCliente == clientCode,
                        cancellationToken);

                if ((promotion.LimiteUsosTotal.HasValue &&
                     totalUsageCount >= promotion.LimiteUsosTotal.Value) ||
                    clientUsageCount >= promotion.LimiteUsosCliente)
                {
                    return Failure(
                        CreateOrderStatus.PromotionNotApplicable,
                        "La promoción alcanzó su límite total o el límite permitido para el cliente.");
                }

                var promotedProductCodes = await _dbContext.PromocionesProductos
                    .AsNoTracking()
                    .Where(item => item.CodPromocion == promotionCode)
                    .Select(item => item.CodProducto)
                    .ToArrayAsync(cancellationToken);
                var eligibleSubtotal = promotedProductCodes.Length == 0
                    ? subtotal
                    : responseItems
                        .Where(item => promotedProductCodes.Contains(item.ProductCode))
                        .Sum(item => item.Subtotal);

                if (eligibleSubtotal <= 0m)
                {
                    return Failure(
                        CreateOrderStatus.PromotionNotApplicable,
                        "La promoción no aplica a ninguno de los productos del pedido.");
                }

                discountApplied = promotion.TipoDescuento == "PERCENTAGE"
                    ? decimal.Round(
                        eligibleSubtotal * promotion.ValorDescuento / 100m,
                        2,
                        MidpointRounding.AwayFromZero)
                    : Math.Min(promotion.ValorDescuento, eligibleSubtotal);

                if (promotion.MontoMaximoDescuento.HasValue)
                {
                    discountApplied = Math.Min(
                        discountApplied,
                        promotion.MontoMaximoDescuento.Value);
                }

                discountApplied = Math.Min(discountApplied, subtotal);
                if (discountApplied <= 0m)
                {
                    return Failure(
                        CreateOrderStatus.PromotionNotApplicable,
                        "La promoción no genera un descuento válido para este pedido.");
                }
            }

            var extraKm = Math.Max(
                0m,
                Math.Ceiling((decimal)distanceKm - IncludedDeliveryKm));
            var deliveryCost = decimal.Round(
                configuration.TarifaBase +
                (extraKm * configuration.TarifaPorKmExtra),
                2,
                MidpointRounding.AwayFromZero);
            var netProductAmount = subtotal - discountApplied;
            var platformCommission = decimal.Round(
                netProductAmount * PlatformCommissionRate,
                2,
                MidpointRounding.AwayFromZero);
            var pharmacySettlement = netProductAmount - platformCommission;
            var totalCharged = netProductAmount + deliveryCost;

            var order = new Pedido
            {
                CodPedido = CreateOrderCode(),
                CodCliente = clientCode,
                CodSucursal = branchCode,
                CodRepartidor = null,
                IdDireccionEntrega = address.IdDireccion,
                CodPromocion = promotionCode,
                CodTipoPago = paymentTypeCode,
                TransaccionPaypalId = null,
                Subtotal = subtotal,
                CostoEnvio = deliveryCost,
                DescuentoAplicado = discountApplied,
                TotalCobrado = totalCharged,
                ComisionPlataforma = platformCommission,
                PagoRepartidor = deliveryCost,
                MontoLiquidarFarmacia = pharmacySettlement,
                EstadoPedido = "Pendiente",
                FechaCreacion = nowUtc,
                FechaEntrega = null,
                NotasCliente = string.IsNullOrWhiteSpace(request.Notes)
                    ? null
                    : request.Notes.Trim()
            };

            foreach (var item in responseItems)
            {
                order.Detalles.Add(new PedidoDetalle
                {
                    CodProducto = item.ProductCode,
                    Cantidad = item.Quantity,
                    PrecioUnitario = item.UnitPrice,
                    Subtotal = item.Subtotal,
                    RequiereReceta = productByCode[item.ProductCode].RequiereReceta
                });

                var inventory = inventories.First(entry =>
                    entry.CodProducto.Equals(
                        item.ProductCode,
                        StringComparison.OrdinalIgnoreCase));

                inventory.CantidadActual -= item.Quantity;
                inventory.UltimaActualizacion = nowUtc;
            }

            order.Seguimientos.Add(new SeguimientoPedido
            {
                EstadoPedido = "Creado",
                Descripcion = "El cliente creó el pedido.",
                FechaHora = nowUtc,
                ActualizadoPor = clientCode
            });

            _dbContext.Pedidos.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (promotion is not null)
            {
                _dbContext.PromocionesUsos.Add(new PromocionUso
                {
                    CodPromocion = promotion.CodPromocion,
                    CodCliente = clientCode,
                    IdPedido = order.IdPedido,
                    DescuentoAplicado = discountApplied,
                    UsadoElUtc = nowUtc
                });
            }

            if (prescription is not null)
            {
                prescription.Estado = "USED";
                prescription.IdPedido = order.IdPedido;
                prescription.UsadoElUtc = nowUtc;
            }

            foreach (var item in responseItems)
            {
                _dbContext.Kardex.Add(new KardexMovimiento
                {
                    CodProducto = item.ProductCode,
                    CodSucursal = branchCode,
                    TipoMovimiento = "Salida",
                    Cantidad = item.Quantity,
                    FechaMovimiento = nowUtc,
                    ReferenciaId = order.IdPedido,
                    Observacion = $"Pedido {order.CodPedido} creado por API."
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreateOrderResult(
                CreateOrderStatus.Success,
                new CreateOrderResponse(
                    order.IdPedido,
                    order.CodPedido,
                    order.CodCliente,
                    order.CodSucursal,
                    order.IdDireccionEntrega,
                    order.EstadoPedido,
                    order.Subtotal,
                    order.CostoEnvio,
                    order.TotalCobrado,
                    order.FechaCreacion,
                    responseItems,
                    discountApplied,
                    promotionCode,
                    prescription?.IdReceta));
        });
    }

    public async Task<UpdateOrderStatusResult> UpdateStatusAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool canManageAnyOrder,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await _dbContext.Pedidos
            .SingleOrDefaultAsync(
                item => item.IdPedido == orderId,
                cancellationToken);

        if (order is null)
        {
            return new UpdateOrderStatusResult(
                UpdateOrderStatusStatus.NotFound,
                Detail: "No se encontró el pedido.");
        }

        var newStatus = request.Status.Trim();
        var isAssignedDriver =
            actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(order.CodRepartidor) &&
            order.CodRepartidor.Equals(
                actorReferenceId,
                StringComparison.OrdinalIgnoreCase);

        var driverCanChange =
            isAssignedDriver &&
            newStatus is "En Preparación" or "En Camino" or "Entregado";

        if (!canManageAnyOrder && !driverCanChange)
        {
            return new UpdateOrderStatusResult(
                UpdateOrderStatusStatus.Forbidden,
                Detail: "La cuenta no puede cambiar el estado de este pedido.");
        }

        if (order.CodTipoPago == "PAGO-PAYPAL" &&
            !await _dbContext.PagosPedidos.AsNoTracking().AnyAsync(
                item => item.IdPedido == order.IdPedido &&
                        item.Estado == "COMPLETED",
                cancellationToken))
        {
            return new UpdateOrderStatusResult(
                UpdateOrderStatusStatus.InvalidTransition,
                Detail: "El pedido PayPal debe estar pagado antes de avanzar su estado.");
        }

        if (!IsValidTransition(order.EstadoPedido, newStatus))
        {
            return new UpdateOrderStatusResult(
                UpdateOrderStatusStatus.InvalidTransition,
                Detail:
                    $"No se puede cambiar de {order.EstadoPedido} a {newStatus}.");
        }

        var previousStatus = order.EstadoPedido;
        var nowUtc = DateTime.UtcNow;

        order.EstadoPedido = newStatus;

        if (newStatus == "Entregado")
        {
            order.FechaEntrega = nowUtc;

            if (!string.IsNullOrWhiteSpace(order.CodRepartidor))
            {
                var driver = await _dbContext.Repartidores
                    .SingleOrDefaultAsync(
                        item =>
                            item.CodRepartidor == order.CodRepartidor &&
                            item.Estado,
                        cancellationToken);

                if (driver is not null)
                {
                    driver.EstadoDisponibilidad = "Disponible";
                }
            }
        }

        _dbContext.SeguimientosPedido.Add(new SeguimientoPedido
        {
            IdPedido = order.IdPedido,
            EstadoPedido = newStatus,
            Descripcion = string.IsNullOrWhiteSpace(request.Description)
                ? $"El pedido cambió al estado {newStatus}."
                : request.Description.Trim(),
            FechaHora = nowUtc,
            ActualizadoPor = actorReferenceId
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateOrderStatusResult(
            UpdateOrderStatusStatus.Success,
            new UpdateOrderStatusResponse(
                order.IdPedido,
                order.CodPedido,
                previousStatus,
                order.EstadoPedido,
                order.CodRepartidor,
                nowUtc,
                order.FechaEntrega));
    }

    public async Task<AssignDriverResult> AssignDriverAsync(
        int orderId,
        string actorReferenceId,
        AssignDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            var order = await _dbContext.Pedidos
                .SingleOrDefaultAsync(
                    item => item.IdPedido == orderId,
                    cancellationToken);

            if (order is null)
            {
                return new AssignDriverResult(
                    AssignDriverStatus.OrderNotFound,
                    Detail: "No se encontró el pedido.");
            }

            if (order.EstadoPedido != "Pendiente" ||
                !string.IsNullOrWhiteSpace(order.CodRepartidor))
            {
                return new AssignDriverResult(
                    AssignDriverStatus.InvalidOrderState,
                    Detail: "El pedido ya fue asignado o no está pendiente.");
            }

            if (order.CodTipoPago == "PAGO-PAYPAL" &&
                !await _dbContext.PagosPedidos.AsNoTracking().AnyAsync(
                    item => item.IdPedido == order.IdPedido &&
                            item.Estado == "COMPLETED",
                    cancellationToken))
            {
                return new AssignDriverResult(
                    AssignDriverStatus.InvalidOrderState,
                    Detail: "El pedido PayPal debe estar pagado antes de asignar un repartidor.");
            }

            var driverCode = request.DriverCode.Trim();
            var driver = await _dbContext.Repartidores
                .SingleOrDefaultAsync(
                    item =>
                        item.CodRepartidor == driverCode &&
                        item.Estado,
                    cancellationToken);

            if (driver is null)
            {
                return new AssignDriverResult(
                    AssignDriverStatus.DriverNotFound,
                    Detail: "No se encontró un repartidor activo con ese código.");
            }

            var hasActiveOrder = await _dbContext.Pedidos
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.CodRepartidor == driverCode &&
                        item.IdPedido != orderId &&
                        item.EstadoPedido != "Entregado",
                    cancellationToken);

            if (!driver.EstadoDisponibilidad.Equals(
                    "Disponible",
                    StringComparison.OrdinalIgnoreCase) ||
                hasActiveOrder)
            {
                return new AssignDriverResult(
                    AssignDriverStatus.DriverUnavailable,
                    Detail: "El repartidor no está disponible para otro pedido.");
            }

            var nowUtc = DateTime.UtcNow;

            order.CodRepartidor = driver.CodRepartidor;
            order.EstadoPedido = "Aceptado";
            driver.EstadoDisponibilidad = "Ocupado";

            _dbContext.SeguimientosPedido.Add(new SeguimientoPedido
            {
                IdPedido = order.IdPedido,
                EstadoPedido = "Aceptado",
                Descripcion = $"Pedido asignado al repartidor {driver.CodRepartidor}.",
                FechaHora = nowUtc,
                ActualizadoPor = actorReferenceId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AssignDriverResult(
                AssignDriverStatus.Success,
                new AssignDriverResponse(
                    order.IdPedido,
                    order.CodPedido,
                    driver.CodRepartidor,
                    order.EstadoPedido,
                    nowUtc));
        });
    }

    private static CreateOrderResult Failure(
        CreateOrderStatus status,
        string detail) =>
        new(status, Detail: detail);

    private async Task<IQueryable<Pedido>?> CreateAuthorizedOrderQueryAsync(
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        bool canViewManagedOrders,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return null;
        }

        var orders = _dbContext.Pedidos.AsNoTracking();

        if (actorType.Equals("Cliente", StringComparison.OrdinalIgnoreCase))
        {
            return orders.Where(
                item => item.CodCliente == actorReferenceId);
        }

        if (actorType.Equals("Repartidor", StringComparison.OrdinalIgnoreCase))
        {
            return orders.Where(
                item => item.CodRepartidor == actorReferenceId);
        }

        if (!actorType.Equals("Empleado", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (isAdministrator)
        {
            return orders;
        }

        if (!canViewManagedOrders)
        {
            return null;
        }

        var branchCode = await _dbContext.Usuarios
            .AsNoTracking()
            .Where(item =>
                item.CodUsuario == actorReferenceId &&
                item.Estado)
            .Select(item => item.CodSucursal)
            .SingleOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(branchCode)
            ? null
            : orders.Where(item => item.CodSucursal == branchCode);
    }

    private static bool IsValidTransition(
        string currentStatus,
        string newStatus) =>
        (currentStatus, newStatus) switch
        {
            ("Pendiente", "Aceptado") => true,
            ("Aceptado", "En Preparación") => true,
            ("Aceptado", "En Camino") => true,
            ("En Preparación", "En Camino") => true,
            ("En Camino", "Entregado") => true,
            _ => false
        };

    private static string CreateOrderCode()
    {
        var rawCode = $"PED-{Guid.NewGuid():N}";
        return rawCode[..20].ToUpperInvariant();
    }

    private static double CalculateDistanceKm(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude)
    {
        static double ToRadians(decimal degrees) =>
            (double)degrees * Math.PI / 180d;

        var latitude1 = ToRadians(originLatitude);
        var latitude2 = ToRadians(destinationLatitude);
        var latitudeDelta = ToRadians(destinationLatitude - originLatitude);
        var longitudeDelta = ToRadians(destinationLongitude - originLongitude);

        var haversine =
            Math.Pow(Math.Sin(latitudeDelta / 2d), 2d) +
            Math.Cos(latitude1) * Math.Cos(latitude2) *
            Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);

        return EarthRadiusKm * 2d * Math.Atan2(
            Math.Sqrt(haversine),
            Math.Sqrt(1d - haversine));
    }
}
