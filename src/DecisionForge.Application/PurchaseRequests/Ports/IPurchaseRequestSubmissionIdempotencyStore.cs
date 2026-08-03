using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.PurchaseRequests.Ports;

public interface IPurchaseRequestSubmissionIdempotencyStore
{
    Task<PurchaseRequestSubmissionRecord?> FindAsync(
        Guid requesterId,
        IdempotencyKey key,
        CancellationToken cancellationToken);
}
