using DecisionForge.Domain.Policies.Selection;

namespace DecisionForge.Application.Decisions.Ports;

public interface IPolicyDecisionQueries
{
    Task<IReadOnlyList<PolicyEvaluationSource>> ListCandidatesAtAsync(
        DateTimeOffset submissionTimestamp,
        CancellationToken cancellationToken);

    Task<PolicyEvaluationSource?> FindByVersionIdAsync(
        Guid policyVersionId,
        CancellationToken cancellationToken);
}
