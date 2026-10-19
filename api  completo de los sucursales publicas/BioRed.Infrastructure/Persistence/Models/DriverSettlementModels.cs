namespace BioRed.Infrastructure.Persistence.Models;

public sealed class CuentaPagoRepartidor
{
    public string CodRepartidor { get; set; } = string.Empty;
    public string Proveedor { get; set; } = "PAYPAL";
    public string CorreoPaypal { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime CreadoElUtc { get; set; }
    public DateTime ActualizadoElUtc { get; set; }
}

public sealed class LiquidacionRepartidor
{
    public long IdLiquidacion { get; set; }
    public int IdPedido { get; set; }
    public string CodRepartidor { get; set; } = string.Empty;
    public long? IdPago { get; set; }
    public string MetodoOrigen { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal MontoRepartidorLocal { get; set; }
    public string MonedaLocal { get; set; } = "GTQ";
    public decimal? MontoProveedor { get; set; }
    public string? MonedaProveedor { get; set; }
    public decimal? TipoCambio { get; set; }
    public decimal MontoEfectivoCobrado { get; set; }
    public decimal MontoEfectivoARemitir { get; set; }
    public string? CorreoPaypalDestino { get; set; }
    public string? LoteProveedorId { get; set; }
    public string? ItemProveedorId { get; set; }
    public string? TransaccionProveedorId { get; set; }
    public string ClaveIdempotencia { get; set; } = string.Empty;
    public int Intentos { get; set; }
    public DateTime CreadoElUtc { get; set; }
    public DateTime? UltimoIntentoUtc { get; set; }
    public DateTime? CompletadoElUtc { get; set; }
    public DateTime? EfectivoConciliadoElUtc { get; set; }
    public string? EfectivoConciliadoPor { get; set; }
    public string? ReferenciaConciliacion { get; set; }
    public string? Observaciones { get; set; }
    public string? DetalleFallo { get; set; }
}

/// <summary>
/// Conserva el historial de una retención o recuperación asociada a la
/// liquidación de un repartidor. La liquidación original no se altera.
/// </summary>
public sealed class AjusteLiquidacionRepartidor
{
    public long IdAjuste { get; set; }
    public long IdLiquidacion { get; set; }
    public long IdReembolso { get; set; }
    public string Tipo { get; set; } = "REVERSO_REEMBOLSO";
    public string Estado { get; set; } = "PENDIENTE_RECUPERACION";
    public string MetodoRecuperacion { get; set; } = "RECUPERACION_FUTURA";
    public decimal MontoLocal { get; set; }
    public string MonedaLocal { get; set; } = "GTQ";
    public DateTime CreadoElUtc { get; set; }
    public DateTime? ConfirmadoElUtc { get; set; }
    public string? ConfirmadoPor { get; set; }
    public string? ReferenciaConfirmacion { get; set; }
    public string? Observaciones { get; set; }
}
