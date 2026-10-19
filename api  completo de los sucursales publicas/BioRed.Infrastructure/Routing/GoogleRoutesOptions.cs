namespace BioRed.Infrastructure.Routing;

public sealed class GoogleRoutesOptions
{
    public const string SectionName = "GoogleRoutes";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://routes.googleapis.com/";
    public string? ApiKey { get; set; }
}
