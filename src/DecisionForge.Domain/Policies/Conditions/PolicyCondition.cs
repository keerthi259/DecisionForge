using System.Collections.ObjectModel;
using DecisionForge.Domain.Policies.Contracts;

namespace DecisionForge.Domain.Policies.Conditions;

public abstract record PolicyCondition
{
    private protected PolicyCondition()
    {
    }
}

public sealed record PolicyComparisonCondition : PolicyCondition
{
    internal PolicyComparisonCondition(
        string fact,
        PolicyOperator @operator,
        PolicyValue value)
    {
        Fact = fact;
        Operator = @operator;
        Value = value;
    }

    public string Fact { get; }

    public PolicyOperator Operator { get; }

    public PolicyValue Value { get; }
}

public sealed record PolicyMembershipCondition : PolicyCondition
{
    internal PolicyMembershipCondition(
        string fact,
        PolicyOperator @operator,
        IEnumerable<PolicyValue> values)
    {
        Fact = fact;
        Operator = @operator;
        Values = new ReadOnlyCollection<PolicyValue>(values.ToArray());
    }

    public string Fact { get; }

    public PolicyOperator Operator { get; }

    public IReadOnlyList<PolicyValue> Values { get; }
}

public sealed record PolicyExistenceCondition : PolicyCondition
{
    internal PolicyExistenceCondition(string fact, PolicyOperator @operator)
    {
        Fact = fact;
        Operator = @operator;
    }

    public string Fact { get; }

    public PolicyOperator Operator { get; }
}

public sealed record PolicyAllCondition : PolicyCondition
{
    internal PolicyAllCondition(IEnumerable<PolicyCondition> children)
    {
        Children = new ReadOnlyCollection<PolicyCondition>(children.ToArray());
    }

    public IReadOnlyList<PolicyCondition> Children { get; }
}

public sealed record PolicyAnyCondition : PolicyCondition
{
    internal PolicyAnyCondition(IEnumerable<PolicyCondition> children)
    {
        Children = new ReadOnlyCollection<PolicyCondition>(children.ToArray());
    }

    public IReadOnlyList<PolicyCondition> Children { get; }
}

public sealed record PolicyNotCondition : PolicyCondition
{
    internal PolicyNotCondition(PolicyCondition child)
    {
        Child = child;
    }

    public PolicyCondition Child { get; }
}
