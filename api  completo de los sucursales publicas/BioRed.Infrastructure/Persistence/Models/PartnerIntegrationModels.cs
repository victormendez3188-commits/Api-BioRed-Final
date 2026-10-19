namespace BioRed.Infrastructure.Persistence.Models;

public sealed class IntegracionFarmacia
{
    public int IdIntegracion { get; set; }
    public string CodEmpresa { get; set; } = string.Empty;
    public string NombreFarmacia { get; set; } = string.Empty;
    public string UrlBaseApi { get; set; } = string.Empty;
    public string NombreContacto { get; set; } = string.Empty;
    public string CorreoContacto { get; set; } = string.Empty;
    public string EstadoIntegracion { get; set; } = "Pendiente";
    public bool ConexionValidada { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaUltimaValidacion { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public string? ActualizadoPor { get; set; }
}
