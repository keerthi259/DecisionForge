using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Facts;

namespace DecisionForge.Domain.Policies.Evaluation;

internal static class PolicyConditionEvaluator
{
    public static PolicyConditionEvaluation Evaluate(
        PolicyCondition condition,
        PolicyFactSet facts,
        CancellationToken cancellationToken,
        ref int evaluationCount,
        int depth = 1)
    {
        cancellationToken.ThrowIfCancellationRequested();
        evaluationCount++;
        if (depth > PolicyContractLimits.MaximumConditionDepth
            || evaluationCount > PolicyEvaluationLimits.MaximumConditionEvaluations)
        {
            throw new PolicyEvaluationException(
                PolicyEvaluationErrorCodes.ExecutionLimit,
                "$",
                "Policy evaluation exceeded a configured execution limit.");
        }

        return condition switch
        {
            PolicyComparisonCondition comparison => EvaluateComparison(
                comparison,
                facts),
            PolicyMembershipCondition membership => EvaluateMembership(
                membership,
                facts),
            PolicyExistenceCondition existence => EvaluateExistence(existence, facts),
            PolicyAllCondition all => EvaluateLogical(
                PolicyConditionKind.All,
                all.Children,
                facts,
                ref evaluationCount,
                depth,
                cancellationToken),
            PolicyAnyCondition any => EvaluateLogical(
                PolicyConditionKind.Any,
                any.Children,
                facts,
                ref evaluationCount,
                depth,
                cancellationToken),
            PolicyNotCondition not => EvaluateNot(
                not,
                facts,
                ref evaluationCount,
                depth,
                cancellationToken),
            _ => throw new PolicyEvaluationException(
                PolicyEvaluationErrorCodes.InvalidPolicy,
                "$",
                "The policy contains an unsupported condition node."),
        };
    }

    private static PolicyConditionEvaluation EvaluateComparison(
        PolicyComparisonCondition condition,
        PolicyFactSet facts)
    {
        PolicyFact fact = GetRequiredFact(facts, condition.Fact);
        bool result = condition.Operator switch
        {
            PolicyOperator.Equals => AreEqual(fact.Value, condition.Value),
            PolicyOperator.NotEquals => !AreEqual(fact.Value, condition.Value),
            PolicyOperator.GreaterThan => CompareNumbers(fact.Value, condition.Value) > 0,
            PolicyOperator.GreaterThanOrEqual => CompareNumbers(
                fact.Value,
                condition.Value) >= 0,
            PolicyOperator.LessThan => CompareNumbers(fact.Value, condition.Value) < 0,
            PolicyOperator.LessThanOrEqual => CompareNumbers(
                fact.Value,
                condition.Value) <= 0,
            PolicyOperator.Contains => Contains(fact.Value, condition.Value),
            _ => throw InvalidOperator(condition.Fact),
        };

        return Leaf(
            PolicyConditionKind.Comparison,
            condition.Operator,
            result,
            Access(fact));
    }

    private static PolicyConditionEvaluation EvaluateMembership(
        PolicyMembershipCondition condition,
        PolicyFactSet facts)
    {
        PolicyFact fact = GetRequiredFact(facts, condition.Fact);
        bool contains = false;
        foreach (PolicyValue expected in condition.Values)
        {
            contains |= AreEqual(fact.Value, expected);
        }

        bool result = condition.Operator == PolicyOperator.In ? contains : !contains;
        return Leaf(
            PolicyConditionKind.Membership,
            condition.Operator,
            result,
            Access(fact));
    }

    private static PolicyConditionEvaluation EvaluateExistence(
        PolicyExistenceCondition condition,
        PolicyFactSet facts)
    {
        bool exists = facts.TryGet(condition.Fact, out PolicyFact fact);
        PolicyFactValueType type = PolicyFactRegistry.All[condition.Fact].ValueType;
        PolicyFactAccess access = exists
            ? Access(fact)
            : new PolicyFactAccess(condition.Fact, type, exists: false, value: null);
        bool result = condition.Operator == PolicyOperator.Exists ? exists : !exists;
        return Leaf(PolicyConditionKind.Existence, condition.Operator, result, access);
    }

    private static PolicyConditionEvaluation EvaluateLogical(
        PolicyConditionKind kind,
        IReadOnlyList<PolicyCondition> conditions,
        PolicyFactSet facts,
        ref int evaluationCount,
        int depth,
        CancellationToken cancellationToken)
    {
        List<PolicyConditionEvaluation> children = [];
        foreach (PolicyCondition condition in conditions)
        {
            children.Add(Evaluate(
                condition,
                facts,
                cancellationToken,
                ref evaluationCount,
                depth + 1));
        }

        bool result = kind == PolicyConditionKind.All
            ? children.All(child => child.Result)
            : children.Any(child => child.Result);
        return new PolicyConditionEvaluation(kind, null, result, [], children);
    }

    private static PolicyConditionEvaluation EvaluateNot(
        PolicyNotCondition condition,
        PolicyFactSet facts,
        ref int evaluationCount,
        int depth,
        CancellationToken cancellationToken)
    {
        PolicyConditionEvaluation child = Evaluate(
            condition.Child,
            facts,
            cancellationToken,
            ref evaluationCount,
            depth + 1);
        return new PolicyConditionEvaluation(
            PolicyConditionKind.Not,
            null,
            !child.Result,
            [],
            [child]);
    }

    private static PolicyConditionEvaluation Leaf(
        PolicyConditionKind kind,
        PolicyOperator @operator,
        bool result,
        PolicyFactAccess access)
    {
        return new PolicyConditionEvaluation(kind, @operator, result, [access], []);
    }

    private static PolicyFactAccess Access(PolicyFact fact)
    {
        return new PolicyFactAccess(fact.Path, fact.ValueType, exists: true, fact.Value);
    }

    private static PolicyFact GetRequiredFact(PolicyFactSet facts, string path)
    {
        if (facts.TryGet(path, out PolicyFact fact))
        {
            return fact;
        }

        throw new PolicyEvaluationException(
            PolicyEvaluationErrorCodes.MissingFact,
            path,
            "A fact required by policy evaluation is missing.");
    }

    private static bool AreEqual(PolicyValue actual, PolicyValue expected)
    {
        return actual switch
        {
            PolicyStringValue left => string.Equals(
                left.Value,
                ((PolicyStringValue)expected).Value,
                StringComparison.Ordinal),
            PolicyNumberValue left => left.Value == ((PolicyNumberValue)expected).Value,
            PolicyBooleanValue left => left.Value == ((PolicyBooleanValue)expected).Value,
            _ => throw TypeMismatch(),
        };
    }

    private static int CompareNumbers(PolicyValue actual, PolicyValue expected)
    {
        return ((PolicyNumberValue)actual).Value.CompareTo(
            ((PolicyNumberValue)expected).Value);
    }

    private static bool Contains(PolicyValue actual, PolicyValue expected)
    {
        return ((PolicyStringValue)actual).Value.Contains(
            ((PolicyStringValue)expected).Value,
            StringComparison.Ordinal);
    }

    private static PolicyEvaluationException InvalidOperator(string fact)
    {
        return new PolicyEvaluationException(PolicyEvaluationErrorCodes.InvalidPolicy, fact, "The policy uses an invalid operator for evaluation.");
    }

    private static PolicyEvaluationException TypeMismatch()
    {
        return new PolicyEvaluationException(PolicyEvaluationErrorCodes.FactTypeMismatch, "$", "A policy value and evaluation fact have incompatible types.");
    }
}
