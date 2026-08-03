namespace DecisionForge.Application.PurchaseRequests.Ports;

public interface IPurchaseRequestQueries
{
    Task<PurchaseRequestListResult> ListForRequesterAsync(
        Guid requesterId,
        PurchaseRequestPage page,
        CancellationToken cancellationToken);

    Task<PurchaseRequestDetail?> FindDetailForRequesterAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken);
}
