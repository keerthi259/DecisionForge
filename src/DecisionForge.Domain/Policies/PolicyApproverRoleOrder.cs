using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.Policies;

public static class PolicyApproverRoleOrder
{
    private static readonly Dictionary<PolicyApproverRole, int> _rank =
        new()
        {
            [PolicyApproverRole.DepartmentApprover] = 1,
            [PolicyApproverRole.ProcurementApprover] = 2,
            [PolicyApproverRole.SecurityApprover] = 3,
            [PolicyApproverRole.FinanceApprover] = 4,
            [PolicyApproverRole.SeniorApprover] = 5,
        };

    public static IReadOnlyList<PolicyApproverRole> OrderDistinct(
        IEnumerable<PolicyApproverRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        PolicyApproverRole[] materialized = roles.ToArray();
        if (materialized.Any(role => !_rank.ContainsKey(role)))
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "The approver role is not supported.",
                nameof(roles));
        }

        return Array.AsReadOnly(materialized
            .Distinct()
            .OrderBy(role => _rank[role])
            .ToArray());
    }
}
