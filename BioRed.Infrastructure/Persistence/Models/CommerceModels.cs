namespace BioRed.Infrastructure.Persistence.Models;

public sealed class Promocion
{
    public string CodPromocion { get; set; } = string.Empty;
    public string CodEmpresa { get; set; } = string.Empty;
    public string? CodSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string TipoDescuento { get; set; } = string.Empty;
    public decimal ValorDescuento { get; set; }
    public decimal MontoMinimoPedido { get; set; }
    public decimal? MontoMaximoDescuento { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public int? LimiteUsosTotal { get; set; }
    public int LimiteUsosCliente { get; set; }
    public bool Activa { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreadoElUtc { get; set; }
    public string? ActualizadoPor { get; set; }
    public DateTime? ActualizadoElUtc { get; set; }
}

public sealed class PromocionProducto
{
    public string CodPromocion { get; set; } = string.Empty;
    public string CodProducto { get; set; } = string.Empty;
}

public sealed class PromocionUso
{
    public long IdPromocionUso { get; set; }
    public string CodPromocion { get; set; } = string.Empty;
    public string CodCliente { get; set; } = string.Empty;
    public int IdPedido { get; set; }
    public decimal DescuentoAplicado { get; set; }
    public DateTime UsadoElUtc { get; set; }
}

public sealed class RecetaCliente
{
    public long IdReceta { get; set; }
    public string CodCliente { get; set; } = string.Empty;
    public string? NumeroReceta { get; set; }
    public string NombreMedico { get; set; } = string.Empty;
    public string NumeroColegiado { get; set; } = string.Empty;
    public DateOnly FechaEmision { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long TamanoArchivo { get; set; }
    public byte[] Documento { get; set; } = Array.Empty<byte>();
    public string HashDocumentoSha256 { get; set; } = string.Empty;
    public string? RevisadoPor { get; set; }
    public DateTime? RevisadoElUtc { get; set; }
    public string? ObservacionesRevision { get; set; }
    public int? IdPedido { get; set; }
    public DateTime CreadoElUtc { get; set; }
    public DateTime? UsadoElUtc { get; set; }
}

public sealed class RecetaProducto
{
    public long IdRecetaProducto { get; set; }
    public long IdReceta { get; set; }
    public string CodProducto { get; set; } = string.Empty;
    public int CantidadAutorizada { get; set; }
}

public sealed class PagoPedido
{
    public long IdPago { get; set; }
    public int IdPedido { get; set; }
    public int? IdCxc { get; set; }
    public string CodCliente { get; set; } = string.Empty;
    public string CodTipoPago { get; set; } = string.Empty;
    public string ProveedorPago { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal MontoLocal { get; set; }
    public string MonedaLocal { get; set; } = string.Empty;
    public decimal MontoProveedor { get; set; }
    public string MonedaProveedor { get; set; } = string.Empty;
    public decimal TipoCambio { get; set; }
    public string? OrdenProveedorId { get; set; }
    public string? CapturaProveedorId { get; set; }
    public string? UrlAprobacion { get; set; }
    public string ClaveIdempotencia { get; set; } = string.Empty;
    public int CreadoPorCuentaId { get; set; }
    public DateTime CreadoElUtc { get; set; }
    public DateTime? CompletadoElUtc { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string? Observaciones { get; set; }
    public string? DetalleFallo { get; set; }
}

public sealed class ComprobantePago
{
    public long IdComprobante { get; set; }
    public long IdPago { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public string CodCliente { get; set; } = string.Empty;
    public string? CorreoDestino { get; set; }
    public DateTime FechaEmisionUtc { get; set; }
    public string EstadoEnvio { get; set; } = "NO_SOLICITADO";
    public int IntentosEnvio { get; set; }
    public DateTime? UltimoEnvioUtc { get; set; }
    public string? DetalleFallo { get; set; }
}

public sealed class ReembolsoPedido
{
    public long IdReembolso { get; set; }
    public int IdDevolucion { get; set; }
    public long? IdPago { get; set; }
    public string ProveedorPago { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal MontoLocal { get; set; }
    public string MonedaLocal { get; set; } = string.Empty;
    public decimal MontoProveedor { get; set; }
    public string MonedaProveedor { get; set; } = string.Empty;
    public decimal TipoCambio { get; set; }
    public string? ReembolsoProveedorId { get; set; }
    public string ClaveIdempotencia { get; set; } = string.Empty;
    public string ProcesadoPor { get; set; } = string.Empty;
    public DateTime CreadoElUtc { get; set; }
    public DateTime? CompletadoElUtc { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string? Observaciones { get; set; }
    public string? DetalleFallo { get; set; }
}
