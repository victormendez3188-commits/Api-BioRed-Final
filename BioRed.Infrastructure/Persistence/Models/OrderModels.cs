namespace BioRed.Infrastructure.Persistence.Models;

public sealed class Producto
{
    public string CodProducto { get; set; } = string.Empty;
    public int IdProducto { get; set; }
    public string CodEmpresa { get; set; } = string.Empty;
    public string NombreProducto { get; set; } = string.Empty;
    public string? DescripcionProducto { get; set; }
    public decimal PrecioCosto { get; set; }
    public decimal PrecioVenta { get; set; }
    public bool RequiereReceta { get; set; }
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
}

public sealed class Inventario
{
    public int IdInventario { get; set; }
    public string CodSucursal { get; set; } = string.Empty;
    public string CodProducto { get; set; } = string.Empty;
    public int CantidadActual { get; set; }
    public int StockMinimo { get; set; }
    public int StockMaximo { get; set; }
    public DateTime UltimaActualizacion { get; set; }
}

public sealed class TipoPago
{
    public string CodTipoPago { get; set; } = string.Empty;
    public int IdTipoPago { get; set; }
    public string NombreTipoPago { get; set; } = string.Empty;
    public bool Estado { get; set; }
}

public sealed class PedidoDetalle
{
    public int IdPedidoDetalle { get; set; }
    public int IdPedido { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public bool? RequiereReceta { get; set; }
    public Pedido Pedido { get; set; } = null!;
}

public sealed class KardexMovimiento
{
    public int IdKardex { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public string TipoMovimiento { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public int? ReferenciaId { get; set; }
    public string? Observacion { get; set; }
}

public sealed class Lote
{
    public int IdLote { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public string NumeroLote { get; set; } = string.Empty;
    public DateOnly FechaVencimiento { get; set; }
    public int CantidadLote { get; set; }
}
