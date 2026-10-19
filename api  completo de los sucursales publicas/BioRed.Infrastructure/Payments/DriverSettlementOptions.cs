namespace BioRed.Infrastructure.Payments;

public sealed class DriverSettlementOptions
{
    public const string SectionName = "DriverSettlements";

    public bool Enabled { get; init; }
    public bool AutoProcessPayPalOnDelivery { get; init; } = true;
    public int ReconciliationIntervalSeconds { get; init; } = 60;
    public string EmailSubject { get; init; } = "Pago de entrega BioRed";
    public string EmailMessage { get; init; } =
        "Has recibido el pago correspondiente a una entrega de BioRed.";
}
