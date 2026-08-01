using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Contracts;

namespace DecisionForge.Domain.Policies.Evaluation;

internal static class PolicyOutcomeAggregator
{
    private static readonly Dictionary<PolicyApproverRole, int> _roleOrder =
        new Dictionary<PolicyApproverRole, int>
        {
            [PolicyApproverRole.DepartmentApprover] = 1,
            [PolicyApproverRole.ProcurementApprover] = 2,
            [PolicyApproverRole.SecurityApprover] = 3,
            [PolicyApproverRole.FinanceApprover] = 4,
            [PolicyApproverRole.SeniorApprover] = 5,
        };

    public static PolicyOutcomeAggregate Aggregate(
        PolicyOutcome defaultOutcome,
        IReadOnlyList<PolicyRuleEvaluation> rules)
    {
        PolicyOutcome[] matched = rules
            .Where(rule => rule.MatchedOutcome is not null)
            .Select(rule => rule.MatchedOutcome!)
            .ToArray();
        bool hasRejection = matched.Any(
            outcome => outcome.Disposition == DecisionDisposition.Rejected);
        bool hasManual = matched.Any(
            outcome => outcome.Disposition == DecisionDisposition.ManualApprovalRequired);
        bool defaultApplied = !hasRejection && !hasManual;
        DecisionDisposition disposition = hasRejection
            ? DecisionDisposition.Rejected
            : hasManual
                ? DecisionDisposition.ManualApprovalRequired
                : defaultOutcome.Disposition;

        IEnumerable<PolicyOutcome> contributing = defaultApplied
            ? matched.Append(defaultOutcome)
            : matched;
        PolicyApproverRole[] roles = contributing
            .SelectMany(outcome => outcome.RequiredApproverRoles)
            .Distinct()
            .OrderBy(role => _roleOrder[role])
            .ToArray();
        List<PolicyEvaluationReason> reasons = [];
        HashSet<string> reasonCodes = new(StringComparer.Ordinal);
        foreach (PolicyOutcome outcome in contributing)
        {
            if (reasonCodes.Add(outcome.ReasonCode.Value))
            {
                reasons.Add(new PolicyEvaluationReason(outcome.ReasonCode, outcome.Message));
            }
        }

        return new PolicyOutcomeAggregate(
            disposition,
            roles,
            reasons,
            defaultApplied);
    }
}

internal sealed record PolicyOutcomeAggregate(
    DecisionDisposition Disposition,
    IReadOnlyList<PolicyApproverRole> RequiredApproverRoles,
    IReadOnlyList<PolicyEvaluationReason> Reasons,
    bool DefaultOutcomeApplied);
