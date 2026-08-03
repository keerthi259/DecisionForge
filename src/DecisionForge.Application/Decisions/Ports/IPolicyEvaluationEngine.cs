using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Application.Decisions.Ports;

public interface IPolicyEvaluationEngine
{
    PolicyEvaluationResult Evaluate(
        PolicyDefinition policy,
        PolicyFactSet facts,
        CancellationToken cancellationToken);
}

public sealed class DeterministicPolicyEvaluationEngine : IPolicyEvaluationEngine
{
    public PolicyEvaluationResult Evaluate(
        PolicyDefinition policy,
        PolicyFactSet facts,
        CancellationToken cancellationToken)
    {
        return PolicyEvaluator.Evaluate(policy, facts, cancellationToken);
    }
}
