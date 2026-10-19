namespace BioRed.Infrastructure.Delivery;

public sealed class DeliveryConfirmationOptions
{
    public const string SectionName = "DeliveryConfirmation";

    public bool Enabled { get; set; } = true;
    public int CodeLifetimeMinutes { get; set; } = 1_440;
    public int MaxAttempts { get; set; } = 5;
}
