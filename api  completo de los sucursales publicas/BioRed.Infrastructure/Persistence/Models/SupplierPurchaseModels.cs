namespace BioRed.Infrastructure.Persistence.Models;

public sealed class Proveedor
{
    public string CodProveedor { get; set; } = string.Empty;
    public int IdProveedor { get; set; }
    public string CodEmpresa { get; set; } = string.Empty;
    public string NombreProveedor { get; set; } = string.Empty;
    public string CuiNit { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string TelefonoProveedor { get; set; } = string.Empty;
    public string EmailProveedor { get; set; } = string.Empty;
    public bool Estado { get; set; }
    public DateTime CreadoEl { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
}

public sealed class CompraEncabezado
{
    public int IdCompra { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string CodUsuario { get; set; } = string.Empty;
    public string CodSucursal { get; set; } = string.Empty;
    public DateTime FechaCompra { get; set; }
    public decimal TotalCompra { get; set; }
    public string CodTipoPago { get; set; } = string.Empty;
    public int IdEstado { get; set; }
    public string? SerieDocumento { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public ICollection<CompraDetalle> Detalles { get; set; } = new List<CompraDetalle>();
}

public sealed class CompraDetalle
{
    public int IdCompraDetalle { get; set; }
    public int IdCompra { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioCosto { get; set; }
    public decimal PrecioVenta { get; set; }
    public string? NumeroLote { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public CompraEncabezado Compra { get; set; } = null!;
}

public sealed class EstadoCatalogo
{
    public int IdEstado { get; set; }
    public string NombreEstado { get; set; } = string.Empty;
    public string? DescripcionEstado { get; set; }
    public bool Activo { get; set; }
}
