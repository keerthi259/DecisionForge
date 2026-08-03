using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.PurchaseRequests.Ports;

public interface IPurchaseRequestRepository
{
    Task<PurchaseRequest?> FindOwnedByIdAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken);

    Task AddAsync(PurchaseRequest purchaseRequest, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
