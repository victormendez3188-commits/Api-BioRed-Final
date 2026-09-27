namespace BioRed.Infrastructure.Persistence.Models;

public sealed class Empresa
{
    public string CodEmpresa { get; set; } = string.Empty;
    public int IdEmpresa { get; set; }
    public string NombreEmpresa { get; set; } = string.Empty;
    public string DireccionEmpresa { get; set; } = string.Empty;
    public string TelefonoEmpresa { get; set; } = string.Empty;
    public string EmailEmpresa { get; set; } = string.Empty;
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
}

public sealed class Sucursal
{
    public string CodSucursal { get; set; } = string.Empty;
    public int IdSucursal { get; set; }
    public string CodEmpresa { get; set; } = string.Empty;
    public string NombreSucursal { get; set; } = string.Empty;
    public string DireccionSucursal { get; set; } = string.Empty;
    public string TelefonoSucursal { get; set; } = string.Empty;
    public string EmailSucursal { get; set; } = string.Empty;
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public ConfiguracionEntregaSucursal? ConfiguracionEntrega { get; set; }
}

public sealed class DireccionCliente
{
    public int IdDireccion { get; set; }
    public string CodCliente { get; set; } = string.Empty;
    public string NombreDireccion { get; set; } = string.Empty;
    public string DireccionCompleta { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public bool? EsPredeterminada { get; set; }
    public bool Estado { get; set; }
}

public sealed class Pedido
{
    public int IdPedido { get; set; }
    public string CodPedido { get; set; } = string.Empty;
    public string CodCliente { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public string? CodRepartidor { get; set; }
    public int IdDireccionEntrega { get; set; }
    public string? CodPromocion { get; set; }
    public string CodTipoPago { get; set; } = string.Empty;
    public string? TransaccionPaypalId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal CostoEnvio { get; set; }
    public decimal? DescuentoAplicado { get; set; }
    public decimal TotalCobrado { get; set; }
    public decimal ComisionPlataforma { get; set; }
    public decimal PagoRepartidor { get; set; }
    public decimal MontoLiquidarFarmacia { get; set; }
    public string EstadoPedido { get; set; } = "Pendiente";
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public string? NotasCliente { get; set; }
    public ICollection<PedidoDetalle> Detalles { get; set; } = new List<PedidoDetalle>();
    public ICollection<UbicacionRepartidor> Ubicaciones { get; set; } = new List<UbicacionRepartidor>();
    public ICollection<SeguimientoPedido> Seguimientos { get; set; } = new List<SeguimientoPedido>();
}

public sealed class UbicacionRepartidor
{
    public long IdUbicacion { get; set; }
    public string CodRepartidor { get; set; } = string.Empty;
    public int? IdPedido { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public decimal? VelocidadKmh { get; set; }
    public decimal? RumboGrados { get; set; }
    public DateTime? FechaDispositivoUtc { get; set; }
    public DateTime FechaRecepcionUtc { get; set; }
    public DateTime FechaRegistro { get; set; }
    public Repartidor Repartidor { get; set; } = null!;
    public Pedido? Pedido { get; set; }
}

public sealed class SeguimientoPedido
{
    public int IdSeguimiento { get; set; }
    public int IdPedido { get; set; }
    public string EstadoPedido { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public DateTime FechaHora { get; set; }
    public string? ActualizadoPor { get; set; }
    public Pedido Pedido { get; set; } = null!;
}

public sealed class ConfiguracionEntregaSucursal
{
    public int IdConfig { get; set; }
    public string CodSucursal { get; set; } = string.Empty;
    public decimal RadioMaximoKm { get; set; }
    public decimal TarifaBase { get; set; }
    public decimal TarifaPorKmExtra { get; set; }
    public int TiempoEstimadoMin { get; set; }
    public bool Activo { get; set; }
    public Sucursal Sucursal { get; set; } = null!;
}
