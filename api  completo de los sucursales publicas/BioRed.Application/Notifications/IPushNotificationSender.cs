namespace BioRed.Application.Notifications;

public interface IPushNotificationSender
{
    Task SendToAccountAsync(
        int accountId,
        string type,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
