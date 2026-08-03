using DecisionForge.Domain.Policies;

namespace DecisionForge.Application.Approvals.Ports;

public interface IApprovalAuthorization
{
    Task<IReadOnlyCollection<PolicyApproverRole>> GetApproverRolesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> CanOverrideDecisionAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
