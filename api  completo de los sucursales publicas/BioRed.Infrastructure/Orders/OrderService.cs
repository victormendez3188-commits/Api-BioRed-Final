using System.Data;
using BioRed.Application.Orders;
using BioRed.Application.Notifications;
using BioRed.Application.Payments;
using BioRed.Infrastructure.Persistence;
using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using BioRed.Infrastructure.Products;

namespace BioRed.Infrastructure.Orders;

public sealed class OrderService : IOrderService
{
    private const double EarthRadiusKm = 6371.0088;
    private const decimal IncludedDeliveryKm = 3m;
    private const decimal PlatformCommissionRate = 0.10m;

    private readonly BioRedDbContext _dbContext;
    private readonly IPushNotificationSender _pushSender;
    private readonly IDriverSettlementService _driverSettlementService;
    private readonly UnitSaleOptions _unitOptions;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        BioRedDbContext dbContext,
        IPushNotificationSender pushSender,
        IDriverSettlementService driverSettlementService,
        IOptions<UnitSaleOptions> unitOptions,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _pushSender = pushSender;
        _driverSettlementService = driverSettlementService;
        _unitOptions = unitOptions.Value;
        _logger = logger;
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
                DeliveryReference = address.Referencia,
                DeliveryLatitude = address.Latitud,
                DeliveryLongitude = address.Longitud,
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
                detail.RequiereReceta ?? false,
                detail.VendidoPorUnidad,
                detail.UnidadBase,
                detail.UnidadesPorPresentacion,
                detail.UnidadesInventario)
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
                header.DeliveryReference,
                header.DeliveryLatitude,
                header.DeliveryLongitude,
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

        var requestedItemGroups = request.Items
            .GroupBy(
                item => item.ProductCode.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                ProductCode = group.Key.ToUpperInvariant(),
                Quantity = group.Sum(item => item.Quantity),
                SaleModes = group
                    .Select(item => item.SellByUnit)
                    .Distinct()
                    .ToArray()
            })
            .ToArray();

        if (requestedItemGroups.Any(item => item.SaleModes.Length != 1))
        {
            return Failure(
                CreateOrderStatus.InvalidRequest,
                "No puede solicitar el mismo producto como presentación y como unidad en el mismo pedido.");
        }

        var requestedItems = requestedItemGroups
            .Select(item => new
            {
                item.ProductCode,
                item.Quantity,
                SellByUnit = item.SaleModes[0]
            })
            .ToArray();

        if (requestedItems.Any(item => item.Quantity > 1000))
        {
            return Failure(
                CreateOrderStatus.InvalidRequest,
                "La cantidad máxima permitida por producto es 1000.");
        }

        if (requestedItems.Any(item => item.SellByUnit) && !_unitOptions.Enabled)
        {
            return Failure(
                CreateOrderStatus.InvalidRequest,
                "La venta por unidad todavía no está habilitada para esta instalación.");
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

            var unitSaleByCode = _unitOptions.Enabled
                ? (await _dbContext.ProductosVentaUnidad
                    .AsNoTracking()
                    .Where(item =>
                        productCodes.Contains(item.CodProducto) &&
                        item.Activo &&
                        item.PermiteVentaUnidad)
                    .ToListAsync(cancellationToken))
                    .ToDictionary(
                        item => item.CodProducto,
                        StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ProductoVentaUnidad>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var requestedItem in requestedItems)
            {
                if (!productByCode.ContainsKey(requestedItem.ProductCode))
                {
                    return Failure(
                        CreateOrderStatus.ProductNotFound,
                        $"El producto {requestedItem.ProductCode} no está disponible para esta farmacia.");
                }

                if (requestedItem.SellByUnit &&
                    (!unitSaleByCode.TryGetValue(
                        requestedItem.ProductCode,
                        out var unitSale) ||
                     unitSale.UnidadesPorPresentacion <= 0 ||
                     unitSale.PrecioPorUnidad <= 0m))
                {
                    return Failure(
                        CreateOrderStatus.ProductNotFound,
                        $"El producto {requestedItem.ProductCode} no está configurado para venta por unidad.");
                }

                if (unitSaleByCode.TryGetValue(
                        requestedItem.ProductCode,
                        out var configuredUnitSale) &&
                    (configuredUnitSale.UnidadesPorPresentacion <= 0 ||
                     configuredUnitSale.PrecioPorUnidad <= 0m))
                {
                    return Failure(
                        CreateOrderStatus.InvalidRequest,
                        $"La configuración de venta por unidad de {requestedItem.ProductCode} no es válida.");
                }
            }

            var inventoryUnitsByCode = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var requestedItem in requestedItems)
            {
                var multiplier =
                    unitSaleByCode.TryGetValue(
                        requestedItem.ProductCode,
                        out var configuredUnitSale) &&
                    !requestedItem.SellByUnit
                        ? configuredUnitSale.UnidadesPorPresentacion
                        : 1;
                var inventoryUnits = (long)requestedItem.Quantity * multiplier;
                if (inventoryUnits <= 0 || inventoryUnits > int.MaxValue)
                {
                    return Failure(
                        CreateOrderStatus.InvalidRequest,
                        $"La cantidad solicitada de {requestedItem.ProductCode} no es válida.");
                }

                inventoryUnitsByCode[requestedItem.ProductCode] =
                    (int)inventoryUnits;
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
                    inventory.CantidadActual <
                    inventoryUnitsByCode[requestedItem.ProductCode])
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
                    unitSaleByCode.TryGetValue(item.ProductCode, out var unitSale);
                    var unitPrice = item.SellByUnit
                        ? unitSale!.PrecioPorUnidad
                        : product.PrecioVenta;
                    var itemSubtotal =
                        decimal.Round(
                            unitPrice * item.Quantity,
                            2,
                            MidpointRounding.AwayFromZero);

                    return new OrderItemResponse(
                        product.CodProducto,
                        product.NombreProducto,
                        item.Quantity,
                        unitPrice,
                        itemSubtotal,
                        item.SellByUnit,
                        unitSale?.UnidadBase,
                        unitSale?.UnidadesPorPresentacion,
                        inventoryUnitsByCode[item.ProductCode]);
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
                    RequiereReceta = productByCode[item.ProductCode].RequiereReceta,
                    VendidoPorUnidad = item.SoldByUnit,
                    UnidadBase = item.BaseUnit,
                    UnidadesPorPresentacion = item.UnitsPerPresentation,
                    UnidadesInventario = item.InventoryUnits
                });

                var inventory = inventories.First(entry =>
                    entry.CodProducto.Equals(
                        item.ProductCode,
                        StringComparison.OrdinalIgnoreCase));

                inventory.CantidadActual -= item.InventoryUnits;
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
                    Cantidad = item.InventoryUnits,
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

        if (isAssignedDriver && newStatus == "Entregado")
        {
            var confirmation = await _dbContext.CodigosEntregaPedido
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.IdPedido == order.IdPedido,
                    cancellationToken);

            // Legacy orders created before this feature do not have a record.
            // New orders with an issued code must be verified by the assigned
            // driver's account before the delivery can be completed.
            if (confirmation is not null)
            {
                var driverAccountId = await _dbContext.CuentasAcceso
                    .AsNoTracking()
                    .Where(item =>
                        item.Estado &&
                        item.TipoUsuario == "Repartidor" &&
                        item.ReferenciaId == actorReferenceId)
                    .Select(item => (int?)item.IdCuenta)
                    .SingleOrDefaultAsync(cancellationToken);

                if (!confirmation.Validado ||
                    !driverAccountId.HasValue ||
                    confirmation.IdCuentaValidacion != driverAccountId.Value)
                {
                    return new UpdateOrderStatusResult(
                        UpdateOrderStatusStatus.InvalidTransition,
                        Detail: "El repartidor debe validar el código de entrega antes de marcar el pedido como Entregado.");
                }
            }
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

        await NotifyReferenceAsync(
            "Cliente",
            order.CodCliente,
            "order_status_changed",
            $"Pedido {order.CodPedido}",
            $"El pedido cambió a {newStatus}.",
            new Dictionary<string, string>
            {
                ["type"] = "order_status_changed",
                ["orderId"] = order.IdPedido.ToString(),
                ["orderCode"] = order.CodPedido,
                ["status"] = newStatus
            },
            cancellationToken);

        if (newStatus == "Entregado" &&
            order.CodTipoPago == "PAGO-PAYPAL")
        {
            try
            {
                var settlement = await _driverSettlementService
                    .EnsurePayPalSettlementAsync(
                        order.IdPedido,
                        cancellationToken);
                if (settlement.Status is not (
                        DriverSettlementStatus.Success or
                        DriverSettlementStatus.ConfigurationError))
                {
                    _logger.LogWarning(
                        "La entrega del pedido {OrderId} se guardó, pero la liquidación del repartidor quedó en estado {SettlementStatus}. Detalle: {SettlementDetail}",
                        order.IdPedido,
                        settlement.Status,
                        settlement.Detail);
                }
            }
            catch (Exception exception)
            {
                // Una falla externa o de configuración no revierte la entrega.
                _logger.LogWarning(
                    exception,
                    "La entrega del pedido {OrderId} se guardó, pero no pudo crearse la liquidación del repartidor.",
                    order.IdPedido);
            }
        }

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

            await NotifyReferenceAsync(
                "Repartidor",
                driver.CodRepartidor,
                "order_assigned",
                "Nuevo pedido asignado",
                $"Se te asignó el pedido {order.CodPedido}.",
                new Dictionary<string, string>
                {
                    ["type"] = "order_assigned",
                    ["orderId"] = order.IdPedido.ToString(),
                    ["orderCode"] = order.CodPedido
                },
                cancellationToken);

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

    public async Task<CancelOrderResult> CancelAsync(
        int orderId,
        string actorType,
        string actorReferenceId,
        bool canManageAnyOrder,
        CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(actorReferenceId))
        {
            return new CancelOrderResult(
                CancelOrderStatus.Forbidden,
                Detail: "La cuenta no está identificada.");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
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
                return new CancelOrderResult(
                    CancelOrderStatus.NotFound,
                    Detail: "No se encontró el pedido.");
            }

            var isOwner = actorType.Equals(
                    "Cliente",
                    StringComparison.OrdinalIgnoreCase) &&
                order.CodCliente.Equals(
                    actorReferenceId,
                    StringComparison.OrdinalIgnoreCase);

            if (!canManageAnyOrder && !isOwner)
            {
                return new CancelOrderResult(
                    CancelOrderStatus.Forbidden,
                    Detail: "Solo el cliente propietario o un administrador puede cancelar este pedido.");
            }

            if (order.EstadoPedido.Equals(
                    "Cancelado",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CancelOrderResult(
                    CancelOrderStatus.InvalidState,
                    Detail: "El pedido ya está cancelado.");
            }

            if (order.EstadoPedido is not ("Pendiente" or "Aceptado"))
            {
                return new CancelOrderResult(
                    CancelOrderStatus.InvalidState,
                    Detail: "El pedido solo puede cancelarse mientras está Pendiente o Aceptado.");
            }

            var completedPayment = await _dbContext.PagosPedidos
                .AsNoTracking()
                .AnyAsync(
                    item => item.IdPedido == orderId &&
                            item.Estado == "COMPLETED",
                    cancellationToken);

            if (completedPayment)
            {
                return new CancelOrderResult(
                    CancelOrderStatus.PaymentCompleted,
                    Detail: "El pedido ya tiene un pago completado. Primero debe procesarse el reembolso correspondiente.");
            }

            var details = await _dbContext.PedidosDetalle
                .Where(item => item.IdPedido == orderId)
                .ToListAsync(cancellationToken);

            var productCodes = details
                .Select(item => item.CodProducto)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var inventories = await _dbContext.Inventarios
                .Where(item =>
                    item.CodSucursal == order.CodSucursal &&
                    productCodes.Contains(item.CodProducto))
                .ToListAsync(cancellationToken);

            foreach (var detail in details)
            {
                var inventory = inventories.SingleOrDefault(item =>
                    item.CodProducto.Equals(
                        detail.CodProducto,
                        StringComparison.OrdinalIgnoreCase));

                if (inventory is null)
                {
                    return new CancelOrderResult(
                        CancelOrderStatus.InvalidState,
                        Detail: $"No existe inventario para restaurar el producto {detail.CodProducto}.");
                }
            }

            var nowUtc = DateTime.UtcNow;
            var previousStatus = order.EstadoPedido;
            var assignedDriverCode = order.CodRepartidor;

            foreach (var detail in details)
            {
                var inventory = inventories.Single(item =>
                    item.CodProducto.Equals(
                        detail.CodProducto,
                        StringComparison.OrdinalIgnoreCase));
                var inventoryUnits = detail.UnidadesInventario > 0
                    ? detail.UnidadesInventario
                    : detail.Cantidad;
                inventory.CantidadActual += inventoryUnits;
                inventory.UltimaActualizacion = nowUtc;

                _dbContext.Kardex.Add(new KardexMovimiento
                {
                    CodProducto = detail.CodProducto,
                    CodSucursal = order.CodSucursal,
                    TipoMovimiento = "EntradaCancelacion",
                    Cantidad = inventoryUnits,
                    FechaMovimiento = nowUtc,
                    ReferenciaId = order.IdPedido,
                    Observacion = $"Pedido {order.CodPedido} cancelado; existencias restauradas."
                });
            }

            var pendingPayments = await _dbContext.PagosPedidos
                .Where(item =>
                    item.IdPedido == order.IdPedido &&
                    (item.Estado == "CREATED" || item.Estado == "APPROVED"))
                .ToListAsync(cancellationToken);

            foreach (var payment in pendingPayments)
            {
                payment.Estado = "FAILED";
                payment.DetalleFallo = "Pedido cancelado antes de completar el pago.";
            }

            var promotionUse = await _dbContext.PromocionesUsos
                .SingleOrDefaultAsync(
                    item => item.IdPedido == order.IdPedido,
                    cancellationToken);

            if (promotionUse is not null)
            {
                _dbContext.PromocionesUsos.Remove(promotionUse);
            }

            var prescription = await _dbContext.RecetasClientes
                .SingleOrDefaultAsync(
                    item => item.IdPedido == order.IdPedido,
                    cancellationToken);

            if (prescription is not null)
            {
                prescription.Estado = "APPROVED";
                prescription.IdPedido = null;
                prescription.UsadoElUtc = null;
            }

            if (!string.IsNullOrWhiteSpace(assignedDriverCode))
            {
                var driver = await _dbContext.Repartidores
                    .SingleOrDefaultAsync(
                        item => item.CodRepartidor == assignedDriverCode && item.Estado,
                        cancellationToken);

                if (driver is not null)
                {
                    var driverHasOtherActiveOrder = await _dbContext.Pedidos
                        .AsNoTracking()
                        .AnyAsync(
                            item => item.IdPedido != order.IdPedido &&
                                    item.CodRepartidor == assignedDriverCode &&
                                    item.EstadoPedido != "Entregado" &&
                                    item.EstadoPedido != "Cancelado",
                            cancellationToken);

                    if (!driverHasOtherActiveOrder)
                    {
                        driver.EstadoDisponibilidad = "Disponible";
                    }
                }
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Pedido cancelado por el cliente."
                : request.Reason.Trim();

            order.EstadoPedido = "Cancelado";
            order.FechaEntrega = null;

            _dbContext.SeguimientosPedido.Add(new SeguimientoPedido
            {
                IdPedido = order.IdPedido,
                EstadoPedido = "Cancelado",
                Descripcion = reason,
                FechaHora = nowUtc,
                ActualizadoPor = actorReferenceId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await NotifyReferenceAsync(
                "Cliente",
                order.CodCliente,
                "order_status_changed",
                $"Pedido {order.CodPedido} cancelado",
                reason,
                new Dictionary<string, string>
                {
                    ["type"] = "order_status_changed",
                    ["orderId"] = order.IdPedido.ToString(),
                    ["orderCode"] = order.CodPedido,
                    ["status"] = "Cancelado"
                },
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(assignedDriverCode))
            {
                await NotifyReferenceAsync(
                    "Repartidor",
                    assignedDriverCode,
                    "order_status_changed",
                    $"Pedido {order.CodPedido} cancelado",
                    reason,
                    new Dictionary<string, string>
                    {
                        ["type"] = "order_status_changed",
                        ["orderId"] = order.IdPedido.ToString(),
                        ["orderCode"] = order.CodPedido,
                        ["status"] = "Cancelado"
                    },
                    cancellationToken);
            }

            return new CancelOrderResult(
                CancelOrderStatus.Success,
                new CancelOrderResponse(
                    order.IdPedido,
                    order.CodPedido,
                    previousStatus,
                    order.EstadoPedido,
                    assignedDriverCode,
                    reason,
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
            ("En Preparación", "En Camino") => true,
            ("En Camino", "Entregado") => true,
            _ => false
        };

    private async Task NotifyReferenceAsync(
        string accountType,
        string referenceId,
        string type,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        var accountId = await _dbContext.CuentasAcceso
            .AsNoTracking()
            .Where(item =>
                item.Estado &&
                item.TipoUsuario == accountType &&
                item.ReferenciaId == referenceId)
            .Select(item => (int?)item.IdCuenta)
            .SingleOrDefaultAsync(cancellationToken);

        if (!accountId.HasValue)
        {
            return;
        }

        await _pushSender.SendToAccountAsync(
            accountId.Value,
            type,
            title,
            body,
            data,
            cancellationToken);
    }

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
