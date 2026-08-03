using DecisionForge.Domain.Policies;

namespace DecisionForge.Application.Approvals.Ports;

public interface IApprovalQueries
{
    Task<ApprovalInboxResult> ListForAuthorizedRolesAsync(
        Guid userId,
        IReadOnlyCollection<PolicyApproverRole> authorizedRoles,
        ApprovalInboxPage page,
        CancellationToken cancellationToken);

    Task<ApprovalWorkflowDetail?> FindForAuthorizedRolesAsync(
        Guid workflowId,
        Guid userId,
        IReadOnlyCollection<PolicyApproverRole> authorizedRoles,
        bool canOverrideDecision,
        CancellationToken cancellationToken);
}
