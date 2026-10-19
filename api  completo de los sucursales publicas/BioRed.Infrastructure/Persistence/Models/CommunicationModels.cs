namespace BioRed.Infrastructure.Persistence.Models;

public sealed class DispositivoNotificacion
{
    public long IdDispositivo { get; set; }
    public int IdCuenta { get; set; }
    public string TokenDispositivo { get; set; } = string.Empty;
    public string Plataforma { get; set; } = string.Empty;
    public string? NombreDispositivo { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaRegistroUtc { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }
    public DateTime? UltimaConexionUtc { get; set; }
    public CuentaAcceso Cuenta { get; set; } = null!;
}

public sealed class MensajePedido
{
    public long IdMensaje { get; set; }
    public int IdPedido { get; set; }
    public int IdCuentaRemitente { get; set; }
    public int IdCuentaDestinatario { get; set; }
    public string TipoMensaje { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaEnvioUtc { get; set; }
    public bool Leido { get; set; }
    public DateTime? FechaLecturaUtc { get; set; }
    public bool Eliminado { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public CuentaAcceso CuentaRemitente { get; set; } = null!;
    public CuentaAcceso CuentaDestinatario { get; set; } = null!;
}
