using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.PurchaseRequests.Ports;

public interface IPurchaseRequestNumberGenerator
{
    Task<RequestNumber> ReserveNextAsync(
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}
