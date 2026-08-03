using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;

namespace DecisionForge.Domain.Policies.Selection;

public static class EffectivePolicySelector
{
    public static PolicyEvaluationSource Select(
        IEnumerable<PolicyEvaluationSource> candidates,
        DateTimeOffset submissionTimestamp)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        PolicyEvaluationSource[] applicable = candidates
            .Where(candidate => candidate is not null)
            .Where(candidate => candidate.IsEffectiveAt(submissionTimestamp))
            .Take(2)
            .ToArray();

        return applicable.Length switch
        {
            1 => applicable[0],
            0 => throw new DomainRuleException(
                DecisionErrorCodes.NoEffectivePolicy,
                "No published policy is effective at the submission timestamp."),
            _ => throw new DomainRuleException(
                DecisionErrorCodes.AmbiguousEffectivePolicy,
                "More than one published policy is effective at the submission timestamp."),
        };
    }
}
