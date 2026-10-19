namespace BioRed.Infrastructure.Persistence.Models;

/// <summary>
/// Stores only a cryptographic hash of the four-digit delivery code. The
/// plaintext code is intentionally never persisted or logged.
/// </summary>
public sealed class CodigoEntregaPedido
{
    public long IdCodigoEntrega { get; set; }
    public int IdPedido { get; set; }
    public string CodigoHash { get; set; } = string.Empty;
    public DateTime FechaGeneracionUtc { get; set; }
    public DateTime FechaExpiracionUtc { get; set; }
    public int IntentosFallidos { get; set; }
    public int MaxIntentos { get; set; }
    public string EstadoEnvio { get; set; } = "PENDIENTE";
    public int IntentosEnvio { get; set; }
    public DateTime? UltimoEnvioUtc { get; set; }
    public string? DetalleFalloEnvio { get; set; }
    public bool Validado { get; set; }
    public int? IdCuentaValidacion { get; set; }
    public DateTime? FechaValidacionUtc { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public CuentaAcceso? CuentaValidacion { get; set; }
}
