namespace BioRed.Infrastructure.Security;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
