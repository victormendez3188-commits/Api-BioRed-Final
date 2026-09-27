namespace BioRed.Infrastructure.Persistence.Models;

public sealed class BitacoraApi
{
    public long IdBitacora { get; set; }
    public int? IdCuenta { get; set; }
    public string? TipoUsuario { get; set; }
    public string? ReferenciaId { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public string MetodoHttp { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public int CodigoEstado { get; set; }
    public long DuracionMs { get; set; }
    public string? DireccionIp { get; set; }
    public string? AgenteUsuario { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; }
}
