namespace BioRed.Infrastructure.Payments;

public sealed class ReceiptEmailOptions
{
    public const string SectionName = "ReceiptEmail";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Security { get; set; } = "StartTls";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "BioRed";
}
