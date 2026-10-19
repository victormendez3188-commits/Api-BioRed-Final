namespace BioRed.Application.Support;

/// <summary>
/// Genera respuestas automáticas seguras para los tickets de atención.
/// Una falla del bot nunca debe impedir que se guarde el mensaje original
/// del cliente.
/// </summary>
public interface ISupportBotService
{
    Task<bool> TryReplyAsync(
        long ticketId,
        int clientAccountId,
        string clientMessage,
        CancellationToken cancellationToken = default);
}
