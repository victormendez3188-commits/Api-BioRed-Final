namespace BioRed.Infrastructure.Support;

/// <summary>
/// Configuración del asistente de atención al cliente basado en respuestas
/// predefinidas. No requiere claves externas ni servicios de IA.
/// </summary>
public sealed class SupportBotOptions
{
    public const string SectionName = "SupportBot";

    public bool Enabled { get; set; } = true;

    public string BotReferenceId { get; set; } = "BOT-SOPORTE";

    public string DisplayName { get; set; } = "Asistente BioRed";
}
