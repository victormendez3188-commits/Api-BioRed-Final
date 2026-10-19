namespace BioRed.Infrastructure.Routing;

/// <summary>
/// Configuración del enrutador gratuito basado en OpenStreetMap/OSRM.
/// No requiere una API key. Para producción con mucho tráfico se recomienda
/// utilizar una instancia propia de OSRM o un proveedor compatible.
/// </summary>
public sealed class OpenStreetMapRoutingOptions
{
    public const string SectionName = "OpenStreetMapRouting";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "https://router.project-osrm.org/";

    public string UserAgent { get; set; } = "BioRed/1.0 (contacto@biored.com)";
}
