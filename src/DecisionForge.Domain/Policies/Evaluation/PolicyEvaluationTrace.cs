using System.Collections.ObjectModel;
using DecisionForge.Domain.Policies.Contracts;

namespace DecisionForge.Domain.Policies.Evaluation;

public enum PolicyConditionKind
{
    Comparison,
    Membership,
    Existence,
    All,
    Any,
    Not,
}

public sealed record PolicyFactAccess
{
    internal PolicyFactAccess(
        string path,
        PolicyFactValueType valueType,
        bool exists,
        PolicyValue? value)
    {
        Path = path;
        ValueType = valueType;
        Exists = exists;
        RawValue = value;
        Value = value is null ? null : PolicyValueFormatter.Format(value);
    }

    public string Path { get; }

    public PolicyFactValueType ValueType { get; }

    public bool Exists { get; }

    public string? Value { get; }

    internal PolicyValue? RawValue { get; }
}

public sealed record PolicyConditionEvaluation
{
    internal PolicyConditionEvaluation(
        PolicyConditionKind kind,
        PolicyOperator? @operator,
        bool result,
        IEnumerable<PolicyFactAccess> factAccesses,
        IEnumerable<PolicyConditionEvaluation> children)
    {
        Kind = kind;
        Operator = @operator;
        Result = result;
        FactAccesses = new ReadOnlyCollection<PolicyFactAccess>(factAccesses.ToArray());
        Children = new ReadOnlyCollection<PolicyConditionEvaluation>(children.ToArray());
    }

    public PolicyConditionKind Kind { get; }

    public PolicyOperator? Operator { get; }

    public bool Result { get; }

    public IReadOnlyList<PolicyFactAccess> FactAccesses { get; }

    public IReadOnlyList<PolicyConditionEvaluation> Children { get; }
}

public sealed record PolicyRuleEvaluation
{
    internal PolicyRuleEvaluation(
        string ruleId,
        int priority,
        PolicyConditionEvaluation condition,
        PolicyOutcome? matchedOutcome)
    {
        RuleId = ruleId;
        Priority = priority;
        Condition = condition;
        MatchedOutcome = matchedOutcome;
    }

    public string RuleId { get; }

    public int Priority { get; }

    public bool Matched => Condition.Result;

    public PolicyConditionEvaluation Condition { get; }

    public PolicyOutcome? MatchedOutcome { get; }
}
