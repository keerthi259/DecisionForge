using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Evaluation;

public static class PolicyEvaluator
{
    public static PolicyEvaluationResult Evaluate(
        PolicyDefinition policy,
        PolicyFactSet facts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(facts);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureValid(policy);

        PolicyChecksum inputChecksum =
            PolicyEvaluationCanonicalSerializer.CalculateInputChecksum(facts);
        PolicyRule[] orderedRules = policy.Rules
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id, StringComparer.Ordinal)
            .ToArray();
        List<PolicyRuleEvaluation> evaluations = new(orderedRules.Length);
        int evaluationCount = 0;
        foreach (PolicyRule rule in orderedRules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PolicyConditionEvaluation condition = PolicyConditionEvaluator.Evaluate(
                rule.When,
                facts,
                cancellationToken,
                ref evaluationCount);
            evaluations.Add(new PolicyRuleEvaluation(
                rule.Id,
                rule.Priority,
                condition,
                condition.Result ? rule.Then : null));
        }

        PolicyOutcomeAggregate outcome = PolicyOutcomeAggregator.Aggregate(
            policy.DefaultOutcome,
            evaluations);
        PolicyChecksum traceChecksum =
            PolicyEvaluationCanonicalSerializer.CalculateTraceChecksum(
                inputChecksum,
                outcome,
                evaluations);
        cancellationToken.ThrowIfCancellationRequested();
        return new PolicyEvaluationResult(
            outcome.Disposition,
            outcome.RequiredApproverRoles,
            outcome.Reasons,
            evaluations,
            outcome.DefaultOutcomeApplied,
            inputChecksum,
            traceChecksum);
    }

    private static void EnsureValid(PolicyDefinition policy)
    {
        if (PolicyValidator.Validate(policy).Count > 0)
        {
            throw new PolicyEvaluationException(
                PolicyEvaluationErrorCodes.InvalidPolicy,
                "$",
                "The policy is invalid and cannot be evaluated.");
        }
    }
}
