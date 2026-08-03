using DecisionForge.Domain.Decisions;

namespace DecisionForge.Application.Decisions.Ports;

public interface IDecisionRepository
{
    Task<Decision?> FindOwnedByPurchaseRequestIdAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken);
}
