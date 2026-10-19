namespace BioRed.Infrastructure.Notifications;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public bool Enabled { get; set; }
    public string? ProjectId { get; set; }
    public string? ServiceAccountJsonBase64 { get; set; }
}
