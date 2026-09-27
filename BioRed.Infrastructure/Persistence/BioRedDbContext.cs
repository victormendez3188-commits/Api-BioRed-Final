using BioRed.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BioRed.Infrastructure.Persistence;

public sealed class BioRedDbContext : DbContext
{
    public BioRedDbContext(DbContextOptions<BioRedDbContext> options)
        : base(options)
    {
    }

    public DbSet<CuentaAcceso> CuentasAcceso => Set<CuentaAcceso>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Repartidor> Repartidores => Set<Repartidor>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<DireccionCliente> DireccionesCliente => Set<DireccionCliente>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<UbicacionRepartidor> UbicacionesRepartidor => Set<UbicacionRepartidor>();
    public DbSet<SeguimientoPedido> SeguimientosPedido => Set<SeguimientoPedido>();
    public DbSet<ConfiguracionEntregaSucursal> ConfiguracionesEntrega => Set<ConfiguracionEntregaSucursal>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Inventario> Inventarios => Set<Inventario>();
    public DbSet<TipoPago> TiposPago => Set<TipoPago>();
    public DbSet<PedidoDetalle> PedidosDetalle => Set<PedidoDetalle>();
    public DbSet<KardexMovimiento> Kardex => Set<KardexMovimiento>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<IntegracionFarmacia> IntegracionesFarmacia => Set<IntegracionFarmacia>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<CompraEncabezado> Compras => Set<CompraEncabezado>();
    public DbSet<CompraDetalle> ComprasDetalle => Set<CompraDetalle>();
    public DbSet<EstadoCatalogo> Estados => Set<EstadoCatalogo>();
    public DbSet<FacturaEncabezadoContable> Facturas => Set<FacturaEncabezadoContable>();
    public DbSet<FacturaDetalleContable> FacturasDetalle => Set<FacturaDetalleContable>();
    public DbSet<DevolucionEncabezadoContable> Devoluciones => Set<DevolucionEncabezadoContable>();
    public DbSet<DevolucionDetalleContable> DevolucionesDetalle => Set<DevolucionDetalleContable>();
    public DbSet<CuentaPorCobrar> CuentasPorCobrar => Set<CuentaPorCobrar>();
    public DbSet<CuentaPorCobrarMovimiento> CuentasPorCobrarDetalle => Set<CuentaPorCobrarMovimiento>();
    public DbSet<CuentaPorPagar> CuentasPorPagar => Set<CuentaPorPagar>();
    public DbSet<CuentaPorPagarMovimiento> CuentasPorPagarDetalle => Set<CuentaPorPagarMovimiento>();
    public DbSet<Promocion> Promociones => Set<Promocion>();
    public DbSet<PromocionProducto> PromocionesProductos => Set<PromocionProducto>();
    public DbSet<PromocionUso> PromocionesUsos => Set<PromocionUso>();
    public DbSet<RecetaCliente> RecetasClientes => Set<RecetaCliente>();
    public DbSet<RecetaProducto> RecetasProductos => Set<RecetaProducto>();
    public DbSet<PagoPedido> PagosPedidos => Set<PagoPedido>();
    public DbSet<ComprobantePago> ComprobantesPago => Set<ComprobantePago>();
    public DbSet<ReembolsoPedido> ReembolsosPedidos => Set<ReembolsoPedido>();
    public DbSet<BitacoraApi> BitacoraApi => Set<BitacoraApi>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CuentaAcceso>(entity =>
        {
            entity.ToTable("Cuentas_Acceso");
            entity.HasKey(item => item.IdCuenta);
            entity.Property(item => item.IdCuenta).HasColumnName("id_cuenta");
            entity.Property(item => item.Correo).HasColumnName("correo").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsUnicode(false);
            entity.Property(item => item.TipoUsuario).HasColumnName("tipo_usuario").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.ReferenciaId).HasColumnName("referencia_id").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado").HasDefaultValue(true);
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(item => item.RequiereCambioPassword).HasColumnName("requiere_cambio_password").HasDefaultValue(false);
            entity.Property(item => item.PasswordTemporalExpiraUtc).HasColumnName("password_temporal_expira_utc").HasColumnType("datetime2(7)");
            entity.Property(item => item.PasswordActualizadaUtc).HasColumnName("password_actualizada_utc").HasColumnType("datetime2(7)");
            entity.Property(item => item.EstadoEnvioPasswordTemporal).HasColumnName("estado_envio_password_temporal").HasMaxLength(20).IsUnicode(false).HasDefaultValue("NO_APLICA");
            entity.Property(item => item.IntentosEnvioPasswordTemporal).HasColumnName("intentos_envio_password_temporal").HasDefaultValue(0);
            entity.Property(item => item.UltimoEnvioPasswordTemporalUtc).HasColumnName("ultimo_envio_password_temporal_utc").HasColumnType("datetime2(7)");
            entity.Property(item => item.DetalleFalloPasswordTemporal).HasColumnName("detalle_fallo_password_temporal").HasMaxLength(500).IsUnicode(false);
            entity.HasIndex(item => item.Correo).IsUnique();
            entity.HasIndex(item => new { item.TipoUsuario, item.ReferenciaId }).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("Refresh_Tokens");
            entity.HasKey(item => item.IdToken);
            entity.Property(item => item.IdToken).HasColumnName("id_token");
            entity.Property(item => item.IdCuenta).HasColumnName("id_cuenta");
            entity.Property(item => item.Token).HasColumnName("token").HasMaxLength(255).IsUnicode(false);
            entity.Property(item => item.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("datetime").HasDefaultValueSql("(getdate())");
            entity.Property(item => item.FechaExpiracion).HasColumnName("fecha_expiracion").HasColumnType("datetime");
            entity.Property(item => item.Revocado).HasColumnName("revocado").HasDefaultValue(false);
            entity.HasIndex(item => item.Token).IsUnique();
            entity.HasOne(item => item.Cuenta)
                .WithMany(item => item.RefreshTokens)
                .HasForeignKey(item => item.IdCuenta)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(item => item.CodUsuario);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdUsuario).HasColumnName("id_usuario").ValueGeneratedOnAdd();
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodRol).HasColumnName("cod_rol").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Pass).HasColumnName("pass").HasMaxLength(75).IsUnicode(false);
            entity.Property(item => item.NombreUsuario).HasColumnName("nombre_usuario").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.ApellidoUsuario).HasColumnName("apellido_usuario").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.EmailUsuario).HasColumnName("email_usuario").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.PrimerCambio).HasColumnName("primer_cambio");
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("Clientes");
            entity.HasKey(item => item.CodCliente);
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdCliente).HasColumnName("id_cliente").ValueGeneratedOnAdd();
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombreCliente).HasColumnName("nombre_cliente").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.ApellidoCliente).HasColumnName("apellido_cliente").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.CuiNit).HasColumnName("CUI_NIT").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.TelefonoCliente).HasColumnName("telefono_cliente").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.EmailCliente).HasColumnName("email_cliente").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
            entity.Property(item => item.CreadoPor).HasColumnName("creado_por").HasMaxLength(15).IsUnicode(false);
            entity.HasIndex(item => item.CuiNit).IsUnique();
            entity.HasIndex(item => item.EmailCliente)
                .IsUnique()
                .HasFilter("[email_cliente] IS NOT NULL");
        });

        modelBuilder.Entity<Repartidor>(entity =>
        {
            entity.ToTable("Repartidores");
            entity.HasKey(item => item.CodRepartidor);
            entity.Property(item => item.CodRepartidor).HasColumnName("cod_repartidor").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdRepartidor).HasColumnName("id_repartidor").ValueGeneratedOnAdd();
            entity.Property(item => item.NombreRepartidor).HasColumnName("nombre_repartidor").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.ApellidosRepartidor).HasColumnName("apellidos_repartidor").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Telefono).HasColumnName("telefono").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.DpiDocumento).HasColumnName("DPI_documento").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.TipoVehiculo).HasColumnName("tipo_vehiculo").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.PlacaVehiculo).HasColumnName("placa_vehiculo").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.LatitudActual).HasColumnName("latitud_actual").HasPrecision(10, 8);
            entity.Property(item => item.LongitudActual).HasColumnName("longitud_actual").HasPrecision(11, 8);
            entity.Property(item => item.EstadoDisponibilidad).HasColumnName("estado_disponibilidad").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
            entity.HasIndex(item => item.DpiDocumento).IsUnique();
            entity.HasIndex(item => item.PlacaVehiculo)
                .IsUnique()
                .HasFilter("[placa_vehiculo] IS NOT NULL AND [placa_vehiculo] <> ''");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("Rol");
            entity.HasKey(item => item.CodRol);
            entity.Property(item => item.CodRol).HasColumnName("cod_rol").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdRol).HasColumnName("id_rol").ValueGeneratedOnAdd();
            entity.Property(item => item.NombreRol).HasColumnName("nombre_rol").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.Nivel).HasColumnName("nivel");
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
        });

        modelBuilder.Entity<Permiso>(entity =>
        {
            entity.ToTable("Permisos");
            entity.HasKey(item => item.CodPermiso);
            entity.Property(item => item.CodPermiso).HasColumnName("cod_permiso").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombrePermiso).HasColumnName("nombre_permiso").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.Descripcion).HasColumnName("descripcion").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.Modulo).HasColumnName("modulo").HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<RolPermiso>(entity =>
        {
            entity.ToTable("Rol_Permisos");
            entity.HasKey(item => new { item.CodRol, item.CodPermiso });
            entity.Property(item => item.CodRol).HasColumnName("cod_rol").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodPermiso).HasColumnName("cod_permiso").HasMaxLength(15).IsUnicode(false);
            entity.HasOne(item => item.Rol).WithMany(item => item.Permisos).HasForeignKey(item => item.CodRol);
            entity.HasOne(item => item.Permiso).WithMany(item => item.Roles).HasForeignKey(item => item.CodPermiso);
        });

        modelBuilder.Entity<Empresa>(entity =>
        {
            entity.ToTable("Empresa");
            entity.HasKey(item => item.CodEmpresa);
            entity.Property(item => item.CodEmpresa).HasColumnName("cod_empresa").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdEmpresa).HasColumnName("id_empresa").ValueGeneratedOnAdd();
            entity.Property(item => item.NombreEmpresa).HasColumnName("nombre_empresa").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.DireccionEmpresa).HasColumnName("direccion_empresa").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.TelefonoEmpresa).HasColumnName("telefono_empresa").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.EmailEmpresa).HasColumnName("email_empresa").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
            entity.HasIndex(item => item.EmailEmpresa).IsUnique();
        });

        modelBuilder.Entity<Sucursal>(entity =>
        {
            entity.ToTable("Sucursal");
            entity.HasKey(item => item.CodSucursal);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdSucursal).HasColumnName("id_sucursal").ValueGeneratedOnAdd();
            entity.Property(item => item.CodEmpresa).HasColumnName("cod_empresa").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombreSucursal).HasColumnName("nombre_sucursal").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.DireccionSucursal).HasColumnName("direccion_sucursal").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.TelefonoSucursal).HasColumnName("telefono_sucursal").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.EmailSucursal).HasColumnName("email_sucursal").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Latitud).HasColumnName("latitud").HasPrecision(10, 8);
            entity.Property(item => item.Longitud).HasColumnName("longitud").HasPrecision(11, 8);
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
            entity.HasIndex(item => item.EmailSucursal).IsUnique();
        });

        modelBuilder.Entity<DireccionCliente>(entity =>
        {
            entity.ToTable("Direcciones_Cliente");
            entity.HasKey(item => item.IdDireccion);
            entity.Property(item => item.IdDireccion).HasColumnName("id_direccion");
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombreDireccion).HasColumnName("nombre_direccion").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.DireccionCompleta).HasColumnName("direccion_completa").HasMaxLength(255).IsUnicode(false);
            entity.Property(item => item.Referencia).HasColumnName("referencia").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.Latitud).HasColumnName("latitud").HasPrecision(10, 8);
            entity.Property(item => item.Longitud).HasColumnName("longitud").HasPrecision(11, 8);
            entity.Property(item => item.EsPredeterminada).HasColumnName("es_predeterminada");
            entity.Property(item => item.Estado).HasColumnName("estado");
        });

        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("Pedidos");
            entity.HasKey(item => item.IdPedido);
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.CodPedido).HasColumnName("cod_pedido").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodRepartidor).HasColumnName("cod_repartidor").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdDireccionEntrega).HasColumnName("id_direccion_entrega");
            entity.Property(item => item.CodPromocion).HasColumnName("cod_promocion").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.TransaccionPaypalId).HasColumnName("transaccion_paypal_id").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
            entity.Property(item => item.CostoEnvio).HasColumnName("costo_envio").HasPrecision(18, 2);
            entity.Property(item => item.DescuentoAplicado).HasColumnName("descuento_aplicado").HasPrecision(18, 2);
            entity.Property(item => item.TotalCobrado).HasColumnName("total_cobrado").HasPrecision(18, 2);
            entity.Property(item => item.ComisionPlataforma).HasColumnName("comision_plataforma").HasPrecision(18, 2);
            entity.Property(item => item.PagoRepartidor).HasColumnName("pago_repartidor").HasPrecision(18, 2);
            entity.Property(item => item.MontoLiquidarFarmacia).HasColumnName("monto_liquidar_farmacia").HasPrecision(18, 2);
            entity.Property(item => item.EstadoPedido).HasColumnName("estado_pedido").HasMaxLength(30).IsUnicode(false);
            entity.Property(item => item.FechaCreacion).HasColumnName("fecha_creacion").HasColumnType("datetime");
            entity.Property(item => item.FechaEntrega).HasColumnName("fecha_entrega").HasColumnType("datetime");
            entity.Property(item => item.NotasCliente).HasColumnName("notas_cliente").HasMaxLength(255).IsUnicode(false);
            entity.HasIndex(item => item.CodPedido).IsUnique();
        });

        modelBuilder.Entity<PedidoDetalle>(entity =>
        {
            entity.ToTable("Pedido_Detalle");
            entity.HasKey(item => item.IdPedidoDetalle);
            entity.Property(item => item.IdPedidoDetalle).HasColumnName("id_pedido_detalle");
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Cantidad).HasColumnName("cantidad");
            entity.Property(item => item.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(18, 2);
            entity.Property(item => item.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
            entity.Property(item => item.RequiereReceta).HasColumnName("requiere_receta");
            entity.HasOne(item => item.Pedido)
                .WithMany(item => item.Detalles)
                .HasForeignKey(item => item.IdPedido);
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(item => item.CodProducto);
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdProducto).HasColumnName("id_producto").ValueGeneratedOnAdd();
            entity.Property(item => item.CodEmpresa).HasColumnName("cod_empresa").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombreProducto).HasColumnName("nombre_producto").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.DescripcionProducto).HasColumnName("descripcion_producto").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.PrecioCosto).HasColumnName("precio_costo").HasPrecision(18, 2);
            entity.Property(item => item.PrecioVenta).HasColumnName("precio_venta").HasPrecision(18, 2);
            entity.Property(item => item.RequiereReceta).HasColumnName("requiere_receta");
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
            entity.Property(item => item.CreadoPor).HasColumnName("creado_por").HasMaxLength(15).IsUnicode(false);
        });

        modelBuilder.Entity<Inventario>(entity =>
        {
            entity.ToTable("Inventario");
            entity.HasKey(item => item.IdInventario);
            entity.Property(item => item.IdInventario).HasColumnName("id_inventario");
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CantidadActual).HasColumnName("cantidad_actual");
            entity.Property(item => item.StockMinimo).HasColumnName("stock_minimo");
            entity.Property(item => item.StockMaximo).HasColumnName("stock_maximo");
            entity.Property(item => item.UltimaActualizacion).HasColumnName("ultima_actualizacion").HasColumnType("datetime");
            entity.HasIndex(item => new { item.CodSucursal, item.CodProducto }).IsUnique();
        });

        modelBuilder.Entity<TipoPago>(entity =>
        {
            entity.ToTable("Tipo_Pago");
            entity.HasKey(item => item.CodTipoPago);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdTipoPago).HasColumnName("id_tipo_pago").ValueGeneratedOnAdd();
            entity.Property(item => item.NombreTipoPago).HasColumnName("nombre_tipo_pago").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado");
        });

        modelBuilder.Entity<KardexMovimiento>(entity =>
        {
            entity.ToTable("Kardex");
            entity.HasKey(item => item.IdKardex);
            entity.Property(item => item.IdKardex).HasColumnName("id_kardex");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.TipoMovimiento).HasColumnName("tipo_movimiento").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.Cantidad).HasColumnName("cantidad");
            entity.Property(item => item.FechaMovimiento).HasColumnName("fecha_movimiento").HasColumnType("datetime");
            entity.Property(item => item.ReferenciaId).HasColumnName("referencia_id");
            entity.Property(item => item.Observacion).HasColumnName("observacion").HasMaxLength(200).IsUnicode(false);
        });

        modelBuilder.Entity<Lote>(entity =>
        {
            entity.ToTable("Lotes");
            entity.HasKey(item => item.IdLote);
            entity.Property(item => item.IdLote).HasColumnName("id_lote");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NumeroLote).HasColumnName("numero_lote").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
            entity.Property(item => item.CantidadLote).HasColumnName("cantidad_lote");
            entity.HasIndex(item => new { item.CodProducto, item.NumeroLote }).IsUnique();
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.ToTable("Proveedores");
            entity.HasKey(item => item.CodProveedor);
            entity.Property(item => item.CodProveedor).HasColumnName("cod_proveedor").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdProveedor).HasColumnName("id_proveedor").ValueGeneratedOnAdd();
            entity.Property(item => item.CodEmpresa).HasColumnName("cod_empresa").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombreProveedor).HasColumnName("nombre_proveedor").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.CuiNit).HasColumnName("CUI_NIT").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.RazonSocial).HasColumnName("razon_social").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.TelefonoProveedor).HasColumnName("telefono_proveedor").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.EmailProveedor).HasColumnName("email_proveedor").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado");
            entity.Property(item => item.CreadoEl).HasColumnName("creado_el").HasColumnType("datetime");
            entity.Property(item => item.CreadoPor).HasColumnName("creado_por").HasMaxLength(15).IsUnicode(false);
            entity.HasIndex(item => item.CuiNit).IsUnique();
            entity.HasIndex(item => item.EmailProveedor).IsUnique();
        });

        modelBuilder.Entity<CompraEncabezado>(entity =>
        {
            entity.ToTable("CompraEncabezado");
            entity.HasKey(item => item.IdCompra);
            entity.Property(item => item.IdCompra).HasColumnName("id_compra");
            entity.Property(item => item.CodProveedor).HasColumnName("cod_proveedor").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.FechaCompra).HasColumnName("fecha_compra").HasColumnType("datetime");
            entity.Property(item => item.TotalCompra).HasColumnName("total_compra").HasPrecision(18, 2);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdEstado).HasColumnName("id_estado");
            entity.Property(item => item.SerieDocumento).HasColumnName("serie_documento").HasMaxLength(30).IsUnicode(false);
            entity.Property(item => item.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.HasMany(item => item.Detalles)
                .WithOne(item => item.Compra)
                .HasForeignKey(item => item.IdCompra);
        });

        modelBuilder.Entity<CompraDetalle>(entity =>
        {
            entity.ToTable("CompraDetalle");
            entity.HasKey(item => item.IdCompraDetalle);
            entity.Property(item => item.IdCompraDetalle).HasColumnName("id_compradetalle");
            entity.Property(item => item.IdCompra).HasColumnName("id_compra");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Cantidad).HasColumnName("cantidad");
            entity.Property(item => item.PrecioCosto).HasColumnName("precio_costo").HasPrecision(18, 2);
            entity.Property(item => item.PrecioVenta).HasColumnName("precio_venta").HasPrecision(18, 2);
            entity.Property(item => item.NumeroLote).HasColumnName("numero_lote").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
        });

        modelBuilder.Entity<EstadoCatalogo>(entity =>
        {
            entity.ToTable("Estados");
            entity.HasKey(item => item.IdEstado);
            entity.Property(item => item.IdEstado).HasColumnName("id_estado");
            entity.Property(item => item.NombreEstado).HasColumnName("nombre_estado").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.DescripcionEstado).HasColumnName("descripcion_estado").HasMaxLength(200).IsUnicode(false);
            entity.Property(item => item.Activo).HasColumnName("activo");
        });

        modelBuilder.Entity<FacturaEncabezadoContable>(entity =>
        {
            entity.ToTable("FacturaEncabezado");
            entity.HasKey(item => item.IdFactura);
            entity.Property(item => item.IdFactura).HasColumnName("id_factura");
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.FechaFactura).HasColumnName("fecha_factura").HasColumnType("datetime");
            entity.Property(item => item.TotalFactura).HasColumnName("total_factura").HasPrecision(18, 2);
            entity.Property(item => item.IdEstado).HasColumnName("id_estado");
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.SerieDocumento).HasColumnName("serie_documento").HasMaxLength(30).IsUnicode(false);
            entity.Property(item => item.NumeroDocumento).HasColumnName("numero_documento").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.Subtotal).HasColumnName("subtotal").HasPrecision(18, 2);
            entity.Property(item => item.CostoEnvio).HasColumnName("costo_envio").HasPrecision(18, 2);
            entity.Property(item => item.Descuento).HasColumnName("descuento").HasPrecision(18, 2);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.HasMany(item => item.Detalles)
                .WithOne(item => item.Factura)
                .HasForeignKey(item => item.IdFactura);
        });

        modelBuilder.Entity<FacturaDetalleContable>(entity =>
        {
            entity.ToTable("FacturaDetalle");
            entity.HasKey(item => item.IdFacturaDetalle);
            entity.Property(item => item.IdFacturaDetalle).HasColumnName("id_facturadetalle");
            entity.Property(item => item.IdFactura).HasColumnName("id_factura");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Cantidad).HasColumnName("cantidad");
            entity.Property(item => item.PrecioCosto).HasColumnName("precio_costo").HasPrecision(18, 2);
            entity.Property(item => item.PrecioVenta).HasColumnName("precio_venta").HasPrecision(18, 2);
        });

        modelBuilder.Entity<DevolucionEncabezadoContable>(entity =>
        {
            entity.ToTable("DevolucionEncabezado");
            entity.HasKey(item => item.IdDevolucion);
            entity.Property(item => item.IdDevolucion).HasColumnName("id_devolucion");
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.FechaDevolucion).HasColumnName("fecha_devolucion").HasColumnType("datetime");
            entity.Property(item => item.TotalDevolucion).HasColumnName("total_devolucion").HasPrecision(18, 2);
            entity.Property(item => item.IdEstado).HasColumnName("id_estado");
            entity.Property(item => item.IdFactura).HasColumnName("id_factura");
            entity.Property(item => item.NumeroDevolucion).HasColumnName("numero_devolucion").HasMaxLength(30).IsUnicode(false);
            entity.Property(item => item.Motivo).HasColumnName("motivo").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.CreditoAplicado).HasColumnName("credito_aplicado").HasPrecision(18, 2);
            entity.Property(item => item.ReembolsoPendiente).HasColumnName("reembolso_pendiente").HasPrecision(18, 2);
            entity.HasMany(item => item.Detalles)
                .WithOne(item => item.Devolucion)
                .HasForeignKey(item => item.IdDevolucion);
        });

        modelBuilder.Entity<DevolucionDetalleContable>(entity =>
        {
            entity.ToTable("DevolucionDetalle");
            entity.HasKey(item => item.IdDevolucionDetalle);
            entity.Property(item => item.IdDevolucionDetalle).HasColumnName("id_devoluciondetalle");
            entity.Property(item => item.IdDevolucion).HasColumnName("id_devolucion");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Cantidad).HasColumnName("cantidad");
            entity.Property(item => item.PrecioCosto).HasColumnName("precio_costo").HasPrecision(18, 2);
            entity.Property(item => item.PrecioVenta).HasColumnName("precio_venta").HasPrecision(18, 2);
            entity.Property(item => item.MontoLinea).HasColumnName("monto_linea").HasPrecision(18, 2);
            entity.Property(item => item.NumeroLote).HasColumnName("numero_lote").HasMaxLength(50).IsUnicode(false);
        });

        modelBuilder.Entity<CuentaPorCobrar>(entity =>
        {
            entity.ToTable("CxCEncabezado");
            entity.HasKey(item => item.IdCxc);
            entity.Property(item => item.IdCxc).HasColumnName("id_cxc");
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.FechaCxc).HasColumnName("fecha_cxc").HasColumnType("datetime");
            entity.Property(item => item.TotalCxc).HasColumnName("total_cxc").HasPrecision(18, 2);
            entity.Property(item => item.IdEstado).HasColumnName("id_estado");
            entity.Property(item => item.IdFactura).HasColumnName("id_factura");
            entity.Property(item => item.SaldoCxc).HasColumnName("saldo_cxc").HasPrecision(18, 2);
            entity.Property(item => item.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
            entity.HasMany(item => item.Movimientos)
                .WithOne(item => item.CuentaPorCobrar)
                .HasForeignKey(item => item.IdCxc);
        });

        modelBuilder.Entity<CuentaPorCobrarMovimiento>(entity =>
        {
            entity.ToTable("CxCDetalle");
            entity.HasKey(item => item.IdCxcDetalle);
            entity.Property(item => item.IdCxcDetalle).HasColumnName("id_cxcdetalle");
            entity.Property(item => item.IdCxc).HasColumnName("id_cxc");
            entity.Property(item => item.IdFactura).HasColumnName("id_factura");
            entity.Property(item => item.MontoPagado).HasColumnName("monto_pagado").HasPrecision(18, 2);
            entity.Property(item => item.FechaPago).HasColumnName("fecha_pago").HasColumnType("datetime");
            entity.Property(item => item.TipoMovimiento).HasColumnName("tipo_movimiento").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.SaldoAnterior).HasColumnName("saldo_anterior").HasPrecision(18, 2);
            entity.Property(item => item.SaldoNuevo).HasColumnName("saldo_nuevo").HasPrecision(18, 2);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.ReferenciaExterna).HasColumnName("referencia_externa").HasMaxLength(100).IsUnicode(false);
        });

        modelBuilder.Entity<CuentaPorPagar>(entity =>
        {
            entity.ToTable("CxPEncabezado");
            entity.HasKey(item => item.IdCxp);
            entity.Property(item => item.IdCxp).HasColumnName("id_cxp");
            entity.Property(item => item.CodProveedor).HasColumnName("cod_proveedor").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.FechaCxp).HasColumnName("fecha_cxp").HasColumnType("datetime");
            entity.Property(item => item.TotalCxp).HasColumnName("total_cxp").HasPrecision(18, 2);
            entity.Property(item => item.IdEstado).HasColumnName("id_estado");
            entity.Property(item => item.IdCompra).HasColumnName("id_compra");
            entity.Property(item => item.SaldoCxp).HasColumnName("saldo_cxp").HasPrecision(18, 2);
            entity.Property(item => item.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
            entity.HasMany(item => item.Movimientos)
                .WithOne(item => item.CuentaPorPagar)
                .HasForeignKey(item => item.IdCxp);
        });

        modelBuilder.Entity<CuentaPorPagarMovimiento>(entity =>
        {
            entity.ToTable("CxPDetalle");
            entity.HasKey(item => item.IdCxpDetalle);
            entity.Property(item => item.IdCxpDetalle).HasColumnName("id_cxpdetalle");
            entity.Property(item => item.IdCxp).HasColumnName("id_cxp");
            entity.Property(item => item.MontoPagado).HasColumnName("monto_pagado").HasPrecision(18, 2);
            entity.Property(item => item.FechaPago).HasColumnName("fecha_pago").HasColumnType("datetime");
            entity.Property(item => item.TipoMovimiento).HasColumnName("tipo_movimiento").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.SaldoAnterior).HasColumnName("saldo_anterior").HasPrecision(18, 2);
            entity.Property(item => item.SaldoNuevo).HasColumnName("saldo_nuevo").HasPrecision(18, 2);
            entity.Property(item => item.CodUsuario).HasColumnName("cod_usuario").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.ReferenciaExterna).HasColumnName("referencia_externa").HasMaxLength(100).IsUnicode(false);
        });

        modelBuilder.Entity<Promocion>(entity =>
        {
            entity.ToTable("Promociones");
            entity.HasKey(item => item.CodPromocion);
            entity.Property(item => item.CodPromocion).HasColumnName("cod_promocion").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodEmpresa).HasColumnName("cod_empresa").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.Nombre).HasColumnName("nombre_promocion").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Descripcion).HasColumnName("descripcion").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.TipoDescuento).HasColumnName("tipo_descuento").HasMaxLength(12).IsUnicode(false);
            entity.Property(item => item.ValorDescuento).HasColumnName("valor_descuento").HasPrecision(18, 2);
            entity.Property(item => item.MontoMinimoPedido).HasColumnName("monto_minimo_pedido").HasPrecision(18, 2);
            entity.Property(item => item.MontoMaximoDescuento).HasColumnName("monto_maximo_descuento").HasPrecision(18, 2);
            entity.Property(item => item.InicioUtc).HasColumnName("fecha_inicio").HasColumnType("datetime");
            entity.Property(item => item.FinUtc).HasColumnName("fecha_fin").HasColumnType("datetime");
            entity.Property(item => item.LimiteUsosTotal).HasColumnName("limite_usos_total");
            entity.Property(item => item.LimiteUsosCliente).HasColumnName("limite_usos_cliente");
            entity.Property(item => item.Activa).HasColumnName("estado");
            entity.Property(item => item.CreadoPor).HasColumnName("creado_por").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CreadoElUtc).HasColumnName("creado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.ActualizadoPor).HasColumnName("actualizado_por").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.ActualizadoElUtc).HasColumnName("actualizado_el_utc").HasColumnType("datetime2");
        });

        modelBuilder.Entity<PromocionProducto>(entity =>
        {
            entity.ToTable("PromocionProductos");
            entity.HasKey(item => new
            {
                item.CodPromocion,
                item.CodProducto
            });

            entity.Property(item => item.CodPromocion)
                .HasColumnName("cod_promocion")
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.Property(item => item.CodProducto)
                .HasColumnName("cod_producto")
                .HasMaxLength(15)
                .IsUnicode(false);

            entity.HasOne<Promocion>()
                .WithMany()
                .HasForeignKey(item => item.CodPromocion)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<Producto>()
                .WithMany()
                .HasForeignKey(item => item.CodProducto)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PromocionUso>(entity =>
        {
            entity.ToTable("PromocionUsos");
            entity.HasKey(item => item.IdPromocionUso);
            entity.Property(item => item.IdPromocionUso).HasColumnName("id_promocion_uso");
            entity.Property(item => item.CodPromocion).HasColumnName("cod_promocion").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.DescuentoAplicado).HasColumnName("descuento_aplicado").HasPrecision(18, 2);
            entity.Property(item => item.UsadoElUtc).HasColumnName("usado_el_utc").HasColumnType("datetime2");
        });

        modelBuilder.Entity<RecetaCliente>(entity =>
        {
            entity.ToTable("RecetasCliente");
            entity.HasKey(item => item.IdReceta);
            entity.Property(item => item.IdReceta).HasColumnName("id_receta");
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NumeroReceta).HasColumnName("numero_receta").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.NombreMedico).HasColumnName("nombre_medico").HasMaxLength(150).IsUnicode(false);
            entity.Property(item => item.NumeroColegiado).HasColumnName("numero_colegiado").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.FechaEmision).HasColumnName("fecha_emision").HasColumnType("date");
            entity.Property(item => item.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
            entity.Property(item => item.Estado).HasColumnName("estado").HasMaxLength(10).IsUnicode(false);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.NombreArchivo).HasColumnName("nombre_archivo").HasMaxLength(150).IsUnicode(false);
            entity.Property(item => item.TipoContenido).HasColumnName("tipo_contenido").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.TamanoArchivo).HasColumnName("tamano_archivo");
            entity.Property(item => item.Documento).HasColumnName("documento");
            entity.Property(item => item.HashDocumentoSha256).HasColumnName("hash_documento_sha256").HasMaxLength(64).IsUnicode(false);
            entity.Property(item => item.RevisadoPor).HasColumnName("revisado_por").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.RevisadoElUtc).HasColumnName("revisado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.ObservacionesRevision).HasColumnName("observaciones_revision").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.CreadoElUtc).HasColumnName("creado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.UsadoElUtc).HasColumnName("usado_el_utc").HasColumnType("datetime2");
        });

        modelBuilder.Entity<RecetaProducto>(entity =>
        {
            entity.ToTable("RecetaProductos");
            entity.HasKey(item => item.IdRecetaProducto);
            entity.Property(item => item.IdRecetaProducto).HasColumnName("id_receta_producto");
            entity.Property(item => item.IdReceta).HasColumnName("id_receta");
            entity.Property(item => item.CodProducto).HasColumnName("cod_producto").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CantidadAutorizada).HasColumnName("cantidad_autorizada");
        });

        modelBuilder.Entity<PagoPedido>(entity =>
        {
            entity.ToTable("PagosPedido");
            entity.HasKey(item => item.IdPago);
            entity.Property(item => item.IdPago).HasColumnName("id_pago");
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.IdCxc).HasColumnName("id_cxc");
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CodTipoPago).HasColumnName("cod_tipo_pago").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.ProveedorPago).HasColumnName("proveedor_pago").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.MontoLocal).HasColumnName("monto_local").HasPrecision(18, 2);
            entity.Property(item => item.MonedaLocal).HasColumnName("moneda_local").HasMaxLength(3).IsUnicode(false);
            entity.Property(item => item.MontoProveedor).HasColumnName("monto_proveedor").HasPrecision(18, 2);
            entity.Property(item => item.MonedaProveedor).HasColumnName("moneda_proveedor").HasMaxLength(3).IsUnicode(false);
            entity.Property(item => item.TipoCambio).HasColumnName("tipo_cambio").HasPrecision(18, 6);
            entity.Property(item => item.OrdenProveedorId).HasColumnName("orden_proveedor_id").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.CapturaProveedorId).HasColumnName("captura_proveedor_id").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.UrlAprobacion).HasColumnName("url_aprobacion").HasMaxLength(1000).IsUnicode(false);
            entity.Property(item => item.ClaveIdempotencia).HasColumnName("clave_idempotencia").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.CreadoPorCuentaId).HasColumnName("creado_por_cuenta");
            entity.Property(item => item.CreadoElUtc).HasColumnName("creado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.CompletadoElUtc).HasColumnName("completado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.ReferenciaExterna).HasColumnName("referencia_externa").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.DetalleFallo).HasColumnName("detalle_fallo").HasMaxLength(500).IsUnicode(false);
        });

        modelBuilder.Entity<ComprobantePago>(entity =>
        {
            entity.ToTable("ComprobantesPago");
            entity.HasKey(item => item.IdComprobante);
            entity.Property(item => item.IdComprobante).HasColumnName("id_comprobante");
            entity.Property(item => item.IdPago).HasColumnName("id_pago");
            entity.Property(item => item.NumeroComprobante).HasColumnName("numero_comprobante").HasMaxLength(40).IsUnicode(false);
            entity.Property(item => item.CodCliente).HasColumnName("cod_cliente").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CorreoDestino).HasColumnName("correo_destino").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.FechaEmisionUtc).HasColumnName("fecha_emision_utc").HasColumnType("datetime2");
            entity.Property(item => item.EstadoEnvio).HasColumnName("estado_envio").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.IntentosEnvio).HasColumnName("intentos_envio");
            entity.Property(item => item.UltimoEnvioUtc).HasColumnName("ultimo_envio_utc").HasColumnType("datetime2");
            entity.Property(item => item.DetalleFallo).HasColumnName("detalle_fallo").HasMaxLength(500).IsUnicode(false);
            entity.HasIndex(item => item.IdPago).IsUnique();
            entity.HasIndex(item => item.NumeroComprobante).IsUnique();
        });

        modelBuilder.Entity<ReembolsoPedido>(entity =>
        {
            entity.ToTable("ReembolsosPedido");
            entity.HasKey(item => item.IdReembolso);
            entity.Property(item => item.IdReembolso).HasColumnName("id_reembolso");
            entity.Property(item => item.IdDevolucion).HasColumnName("id_devolucion");
            entity.Property(item => item.IdPago).HasColumnName("id_pago");
            entity.Property(item => item.ProveedorPago).HasColumnName("proveedor_pago").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.Estado).HasColumnName("estado").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.MontoLocal).HasColumnName("monto_local").HasPrecision(18, 2);
            entity.Property(item => item.MonedaLocal).HasColumnName("moneda_local").HasMaxLength(3).IsUnicode(false);
            entity.Property(item => item.MontoProveedor).HasColumnName("monto_proveedor").HasPrecision(18, 2);
            entity.Property(item => item.MonedaProveedor).HasColumnName("moneda_proveedor").HasMaxLength(3).IsUnicode(false);
            entity.Property(item => item.TipoCambio).HasColumnName("tipo_cambio").HasPrecision(18, 6);
            entity.Property(item => item.ReembolsoProveedorId).HasColumnName("reembolso_proveedor_id").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.ClaveIdempotencia).HasColumnName("clave_idempotencia").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.ProcesadoPor).HasColumnName("procesado_por").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.CreadoElUtc).HasColumnName("creado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.CompletadoElUtc).HasColumnName("completado_el_utc").HasColumnType("datetime2");
            entity.Property(item => item.ReferenciaExterna).HasColumnName("referencia_externa").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.Observaciones).HasColumnName("observaciones").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.DetalleFallo).HasColumnName("detalle_fallo").HasMaxLength(500).IsUnicode(false);
        });

        modelBuilder.Entity<UbicacionRepartidor>(entity =>
        {
            entity.ToTable("Ubicaciones_Repartidor");
            entity.HasKey(item => item.IdUbicacion);
            entity.Property(item => item.IdUbicacion).HasColumnName("id_ubicacion");
            entity.Property(item => item.CodRepartidor).HasColumnName("cod_repartidor").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.Latitud).HasColumnName("latitud").HasPrecision(10, 8);
            entity.Property(item => item.Longitud).HasColumnName("longitud").HasPrecision(11, 8);
            entity.Property(item => item.PrecisionMetros).HasColumnName("precision_metros").HasPrecision(8, 2);
            entity.Property(item => item.VelocidadKmh).HasColumnName("velocidad_kmh").HasPrecision(6, 2);
            entity.Property(item => item.RumboGrados).HasColumnName("rumbo_grados").HasPrecision(6, 2);
            entity.Property(item => item.FechaDispositivoUtc).HasColumnName("fecha_dispositivo_utc").HasColumnType("datetime2");
            entity.Property(item => item.FechaRecepcionUtc).HasColumnName("fecha_recepcion_utc").HasColumnType("datetime2");
            entity.Property(item => item.FechaRegistro).HasColumnName("fecha_registro").HasColumnType("datetime");
            entity.HasOne(item => item.Repartidor).WithMany(item => item.Ubicaciones).HasForeignKey(item => item.CodRepartidor);
            entity.HasOne(item => item.Pedido).WithMany(item => item.Ubicaciones).HasForeignKey(item => item.IdPedido);
        });

        modelBuilder.Entity<SeguimientoPedido>(entity =>
        {
            entity.ToTable("Seguimiento_Pedido");
            entity.HasKey(item => item.IdSeguimiento);
            entity.Property(item => item.IdSeguimiento).HasColumnName("id_seguimiento");
            entity.Property(item => item.IdPedido).HasColumnName("id_pedido");
            entity.Property(item => item.EstadoPedido).HasColumnName("estado_pedido").HasMaxLength(30).IsUnicode(false);
            entity.Property(item => item.Descripcion).HasColumnName("descripcion").HasMaxLength(255).IsUnicode(false);
            entity.Property(item => item.FechaHora).HasColumnName("fecha_hora").HasColumnType("datetime");
            entity.Property(item => item.ActualizadoPor).HasColumnName("actualizado_por").HasMaxLength(15).IsUnicode(false);
            entity.HasOne(item => item.Pedido).WithMany(item => item.Seguimientos).HasForeignKey(item => item.IdPedido);
        });

        modelBuilder.Entity<ConfiguracionEntregaSucursal>(entity =>
        {
            entity.ToTable("Configuracion_Entrega_Sucursal");
            entity.HasKey(item => item.IdConfig);
            entity.Property(item => item.IdConfig).HasColumnName("id_config");
            entity.Property(item => item.CodSucursal).HasColumnName("cod_sucursal").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.RadioMaximoKm).HasColumnName("radio_maximo_km").HasPrecision(5, 2);
            entity.Property(item => item.TarifaBase).HasColumnName("tarifa_base").HasPrecision(18, 2);
            entity.Property(item => item.TarifaPorKmExtra).HasColumnName("tarifa_por_km_extra").HasPrecision(18, 2);
            entity.Property(item => item.TiempoEstimadoMin).HasColumnName("tiempo_estimado_min");
            entity.Property(item => item.Activo).HasColumnName("activo");
            entity.HasIndex(item => item.CodSucursal).IsUnique();
            entity.HasOne(item => item.Sucursal).WithOne(item => item.ConfiguracionEntrega).HasForeignKey<ConfiguracionEntregaSucursal>(item => item.CodSucursal);
        });

        modelBuilder.Entity<BitacoraApi>(entity =>
        {
            entity.ToTable("BitacoraApi");
            entity.HasKey(item => item.IdBitacora);
            entity.Property(item => item.IdBitacora).HasColumnName("id_bitacora");
            entity.Property(item => item.IdCuenta).HasColumnName("id_cuenta");
            entity.Property(item => item.TipoUsuario).HasColumnName("tipo_usuario").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.ReferenciaId).HasColumnName("referencia_id").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.Modulo).HasColumnName("modulo").HasMaxLength(50).IsUnicode(false);
            entity.Property(item => item.MetodoHttp).HasColumnName("metodo_http").HasMaxLength(10).IsUnicode(false);
            entity.Property(item => item.Ruta).HasColumnName("ruta").HasMaxLength(300).IsUnicode(false);
            entity.Property(item => item.CodigoEstado).HasColumnName("codigo_estado");
            entity.Property(item => item.DuracionMs).HasColumnName("duracion_ms");
            entity.Property(item => item.DireccionIp).HasColumnName("direccion_ip").HasMaxLength(45).IsUnicode(false);
            entity.Property(item => item.AgenteUsuario).HasColumnName("agente_usuario").HasMaxLength(500).IsUnicode(false);
            entity.Property(item => item.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.FechaUtc).HasColumnName("fecha_utc").HasColumnType("datetime2");
            entity.HasIndex(item => item.FechaUtc);
            entity.HasIndex(item => new { item.Modulo, item.FechaUtc });
        });

        modelBuilder.Entity<IntegracionFarmacia>(entity =>
        {
            entity.ToTable("Integraciones_Farmacia");
            entity.HasKey(item => item.IdIntegracion);
            entity.Property(item => item.IdIntegracion).HasColumnName("id_integracion");
            entity.Property(item => item.CodEmpresa).HasColumnName("cod_empresa").HasMaxLength(15).IsUnicode(false);
            entity.Property(item => item.NombreFarmacia).HasColumnName("nombre_farmacia").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.UrlBaseApi).HasColumnName("url_base_api").HasMaxLength(500).IsUnicode(false);
            entity.Property(item => item.NombreContacto).HasColumnName("nombre_contacto").HasMaxLength(100).IsUnicode(false);
            entity.Property(item => item.CorreoContacto).HasColumnName("correo_contacto").HasMaxLength(150).IsUnicode(false);
            entity.Property(item => item.EstadoIntegracion).HasColumnName("estado_integracion").HasMaxLength(20).IsUnicode(false);
            entity.Property(item => item.ConexionValidada).HasColumnName("conexion_validada");
            entity.Property(item => item.FechaSolicitud).HasColumnName("fecha_solicitud").HasColumnType("datetime2");
            entity.Property(item => item.FechaUltimaValidacion).HasColumnName("fecha_ultima_validacion").HasColumnType("datetime2");
            entity.Property(item => item.FechaAprobacion).HasColumnName("fecha_aprobacion").HasColumnType("datetime2");
            entity.Property(item => item.ActualizadoPor).HasColumnName("actualizado_por").HasMaxLength(15).IsUnicode(false);
            entity.HasIndex(item => item.CodEmpresa).IsUnique();
        });
    }
}
