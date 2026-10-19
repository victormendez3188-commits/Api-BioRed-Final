namespace BioRed.Infrastructure.Persistence.Models;

public sealed class TicketAtencion
{
    public long IdTicket { get; set; }
    public int IdCuentaCliente { get; set; }
    public int? IdPedido { get; set; }
    public string Asunto { get; set; } = string.Empty;
    public string Categoria { get; set; } = "OTRO";
    public string Estado { get; set; } = "ABIERTO";
    public string Prioridad { get; set; } = "NORMAL";
    public DateTime FechaCreacionUtc { get; set; }
    public DateTime FechaActualizacionUtc { get; set; }
    public DateTime? FechaCierreUtc { get; set; }
    public int? CerradoPorCuentaId { get; set; }
    public CuentaAcceso CuentaCliente { get; set; } = null!;
    public CuentaAcceso? CerradoPorCuenta { get; set; }
    public Pedido? Pedido { get; set; }
    public ICollection<MensajeTicketAtencion> Mensajes { get; set; } =
        new List<MensajeTicketAtencion>();
}

public sealed class MensajeTicketAtencion
{
    public long IdMensaje { get; set; }
    public long IdTicket { get; set; }
    public int IdCuentaRemitente { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaEnvioUtc { get; set; }
    public TicketAtencion Ticket { get; set; } = null!;
    public CuentaAcceso CuentaRemitente { get; set; } = null!;
}
