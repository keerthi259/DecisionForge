using System.Collections.ObjectModel;

namespace DecisionForge.Domain.Policies;

public static class PolicyOperatorNames
{
    public static IReadOnlyDictionary<string, PolicyOperator> All { get; } =
        new ReadOnlyDictionary<string, PolicyOperator>(
            new Dictionary<string, PolicyOperator>(StringComparer.Ordinal)
            {
                ["equals"] = PolicyOperator.Equals,
                ["notEquals"] = PolicyOperator.NotEquals,
                ["greaterThan"] = PolicyOperator.GreaterThan,
                ["greaterThanOrEqual"] = PolicyOperator.GreaterThanOrEqual,
                ["lessThan"] = PolicyOperator.LessThan,
                ["lessThanOrEqual"] = PolicyOperator.LessThanOrEqual,
                ["in"] = PolicyOperator.In,
                ["notIn"] = PolicyOperator.NotIn,
                ["exists"] = PolicyOperator.Exists,
                ["notExists"] = PolicyOperator.NotExists,
                ["contains"] = PolicyOperator.Contains,
            });

    public static bool TryParse(string? value, out PolicyOperator result)
    {
        if (value is not null && All.TryGetValue(value, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    public static string ToJsonName(PolicyOperator value)
    {
        return value switch
        {
            PolicyOperator.Equals => "equals",
            PolicyOperator.NotEquals => "notEquals",
            PolicyOperator.GreaterThan => "greaterThan",
            PolicyOperator.GreaterThanOrEqual => "greaterThanOrEqual",
            PolicyOperator.LessThan => "lessThan",
            PolicyOperator.LessThanOrEqual => "lessThanOrEqual",
            PolicyOperator.In => "in",
            PolicyOperator.NotIn => "notIn",
            PolicyOperator.Exists => "exists",
            PolicyOperator.NotExists => "notExists",
            PolicyOperator.Contains => "contains",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported policy operator."),
        };
    }
}
