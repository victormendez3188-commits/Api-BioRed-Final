namespace BioRed.Infrastructure.Persistence.Models;

public sealed class FacturaEncabezadoContable
{
    public int IdFactura { get; set; }
    public string CodCliente { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public string CodUsuario { get; set; } = string.Empty;
    public string CodTipoPago { get; set; } = string.Empty;
    public DateTime FechaFactura { get; set; }
    public decimal TotalFactura { get; set; }
    public int IdEstado { get; set; }
    public int IdPedido { get; set; }
    public string? SerieDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal CostoEnvio { get; set; }
    public decimal Descuento { get; set; }
    public string? Observaciones { get; set; }
    public ICollection<FacturaDetalleContable> Detalles { get; set; } =
        new List<FacturaDetalleContable>();
}

public sealed class FacturaDetalleContable
{
    public int IdFacturaDetalle { get; set; }
    public int IdFactura { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioCosto { get; set; }
    public decimal PrecioVenta { get; set; }
    public FacturaEncabezadoContable Factura { get; set; } = null!;
}

public sealed class DevolucionEncabezadoContable
{
    public int IdDevolucion { get; set; }
    public string CodCliente { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public string CodUsuario { get; set; } = string.Empty;
    public string CodTipoPago { get; set; } = string.Empty;
    public DateTime FechaDevolucion { get; set; }
    public decimal TotalDevolucion { get; set; }
    public int IdEstado { get; set; }
    public int IdFactura { get; set; }
    public string NumeroDevolucion { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public decimal CreditoAplicado { get; set; }
    public decimal ReembolsoPendiente { get; set; }
    public ICollection<DevolucionDetalleContable> Detalles { get; set; } =
        new List<DevolucionDetalleContable>();
}

public sealed class DevolucionDetalleContable
{
    public int IdDevolucionDetalle { get; set; }
    public int IdDevolucion { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioCosto { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal? MontoLinea { get; set; }
    public string? NumeroLote { get; set; }
    public DevolucionEncabezadoContable Devolucion { get; set; } = null!;
}

public sealed class CuentaPorCobrar
{
    public int IdCxc { get; set; }
    public string CodCliente { get; set; } = string.Empty;
    public string CodUsuario { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public DateTime FechaCxc { get; set; }
    public decimal TotalCxc { get; set; }
    public int IdEstado { get; set; }
    public int IdFactura { get; set; }
    public decimal SaldoCxc { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public ICollection<CuentaPorCobrarMovimiento> Movimientos { get; set; } =
        new List<CuentaPorCobrarMovimiento>();
}

public sealed class CuentaPorCobrarMovimiento
{
    public int IdCxcDetalle { get; set; }
    public int IdCxc { get; set; }
    public int IdFactura { get; set; }
    public decimal MontoPagado { get; set; }
    public DateTime FechaPago { get; set; }
    public string TipoMovimiento { get; set; } = string.Empty;
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoNuevo { get; set; }
    public string CodUsuario { get; set; } = string.Empty;
    public string? CodTipoPago { get; set; }
    public string? Observaciones { get; set; }
    public string? ReferenciaExterna { get; set; }
    public CuentaPorCobrar CuentaPorCobrar { get; set; } = null!;
}

public sealed class CuentaPorPagar
{
    public int IdCxp { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string CodUsuario { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public DateTime FechaCxp { get; set; }
    public decimal TotalCxp { get; set; }
    public int IdEstado { get; set; }
    public int IdCompra { get; set; }
    public decimal SaldoCxp { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public ICollection<CuentaPorPagarMovimiento> Movimientos { get; set; } =
        new List<CuentaPorPagarMovimiento>();
}

public sealed class CuentaPorPagarMovimiento
{
    public int IdCxpDetalle { get; set; }
    public int IdCxp { get; set; }
    public decimal MontoPagado { get; set; }
    public DateTime FechaPago { get; set; }
    public string TipoMovimiento { get; set; } = string.Empty;
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoNuevo { get; set; }
    public string CodUsuario { get; set; } = string.Empty;
    public string? CodTipoPago { get; set; }
    public string? Observaciones { get; set; }
    public string? ReferenciaExterna { get; set; }
    public CuentaPorPagar CuentaPorPagar { get; set; } = null!;
}
