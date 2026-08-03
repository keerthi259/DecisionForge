using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies;

namespace DecisionForge.Domain.Approvals;

public static class ApprovalStagePlanBuilder
{
    public static IReadOnlyList<PolicyApproverRole> Build(
        IEnumerable<PolicyApproverRole> requiredRoles)
    {
        IReadOnlyList<PolicyApproverRole> plan =
            PolicyApproverRoleOrder.OrderDistinct(requiredRoles);
        if (plan.Count == 0)
        {
            throw new DomainRuleException(
                ApprovalErrorCodes.RolesRequired,
                "A manual approval workflow requires at least one approver role.",
                nameof(requiredRoles));
        }

        return plan;
    }
}
