namespace BioRed.Application.Delivery;

public interface IOrderDeliveryConfirmationService
{
    /// <summary>
    /// Creates and sends a code for a newly created order. A delivery failure
    /// must not undo the order itself.
    /// </summary>
    Task<DeliveryCodeDispatchResult> GenerateForNewOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<DeliveryCodeDispatchResult> ResendAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<VerifyDeliveryCodeResult> VerifyAsync(
        int orderId,
        int accountId,
        string actorType,
        string actorReferenceId,
        string code,
        CancellationToken cancellationToken = default);
}
