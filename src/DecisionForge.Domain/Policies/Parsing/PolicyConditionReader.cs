using System.Text.Json;
using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;

namespace DecisionForge.Domain.Policies.Parsing;

internal sealed class PolicyConditionReader
{
    private readonly PolicyJsonReaderContext _context;

    public PolicyConditionReader(PolicyJsonReaderContext context)
    {
        _context = context;
    }

    public PolicyCondition? Read(JsonElement? element, string path, int depth)
    {
        if (depth > PolicyContractLimits.MaximumConditionDepth)
        {
            _context.Error(
                path,
                "policy.limit.condition-depth",
                "The condition tree exceeds the supported depth.");
            return null;
        }

        Dictionary<string, JsonElement>? properties = _context.ReadObject(
            element,
            path,
            ["fact", "operator", "value", "all", "any", "not"],
            []);
        if (properties is null)
        {
            return null;
        }

        bool hasLeaf = properties.Keys.Any(key => key is "fact" or "operator" or "value");
        int nodeKinds = Convert.ToInt32(hasLeaf)
            + Convert.ToInt32(properties.ContainsKey("all"))
            + Convert.ToInt32(properties.ContainsKey("any"))
            + Convert.ToInt32(properties.ContainsKey("not"));
        if (nodeKinds != 1)
        {
            _context.Error(
                path,
                "policy.condition.shape",
                "A condition must contain exactly one supported node shape.");
            return null;
        }

        if (properties.TryGetValue("all", out JsonElement all))
        {
            return ReadLogicalChildren(all, $"{path}.all", depth, isAll: true);
        }

        if (properties.TryGetValue("any", out JsonElement any))
        {
            return ReadLogicalChildren(any, $"{path}.any", depth, isAll: false);
        }

        if (properties.TryGetValue("not", out JsonElement not))
        {
            PolicyCondition? child = Read(not, $"{path}.not", depth + 1);
            return child is null ? null : new PolicyNotCondition(child);
        }

        return ReadLeaf(properties, path);
    }

    private PolicyCondition? ReadLogicalChildren(
        JsonElement element,
        string path,
        int depth,
        bool isAll)
    {
        if (!_context.RequireKind(element, JsonValueKind.Array, path))
        {
            return null;
        }

        int count = element.GetArrayLength();
        if (count is 0 or > PolicyContractLimits.MaximumConditionChildren)
        {
            _context.Error(
                path,
                "policy.limit.children",
                "Logical conditions require a bounded non-empty child list.");
        }

        List<PolicyCondition> children = [];
        int index = 0;
        bool complete = count > 0 && count <= PolicyContractLimits.MaximumConditionChildren;
        foreach (JsonElement value in element.EnumerateArray())
        {
            PolicyCondition? child = Read(value, $"{path}[{index}]", depth + 1);
            complete &= child is not null;
            if (child is not null)
            {
                children.Add(child);
            }

            index++;
        }

        if (!complete)
        {
            return null;
        }

        return isAll ? new PolicyAllCondition(children) : new PolicyAnyCondition(children);
    }

    private PolicyCondition? ReadLeaf(
        Dictionary<string, JsonElement> properties,
        string path)
    {
        if (!properties.ContainsKey("fact"))
        {
            _context.Error(
                $"{path}.fact",
                "policy.json.required",
                "A required JSON property is missing.");
        }

        if (!properties.ContainsKey("operator"))
        {
            _context.Error(
                $"{path}.operator",
                "policy.json.required",
                "A required JSON property is missing.");
        }

        string? fact = _context.ReadString(properties, "fact", path);
        string? operatorName = _context.ReadString(properties, "operator", path);
        if (fact is null || operatorName is null)
        {
            return null;
        }

        if (!PolicyOperatorNames.TryParse(operatorName, out PolicyOperator @operator))
        {
            _context.Error(
                $"{path}.operator",
                "policy.operator.unknown",
                "The policy operator is not supported.");
            return null;
        }

        bool hasValue = properties.TryGetValue("value", out JsonElement value);
        if (@operator is PolicyOperator.Exists or PolicyOperator.NotExists)
        {
            if (hasValue)
            {
                _context.Error(
                    $"{path}.value",
                    "policy.condition.shape",
                    "Existence operators do not accept a comparison value.");
                return null;
            }

            return new PolicyExistenceCondition(fact, @operator);
        }

        if (!hasValue)
        {
            _context.Error(
                $"{path}.value",
                "policy.json.required",
                "A required JSON property is missing.");
            return null;
        }

        return @operator is PolicyOperator.In or PolicyOperator.NotIn
            ? ReadMembership(fact, @operator, value, $"{path}.value")
            : ReadComparison(fact, @operator, value, $"{path}.value");
    }

    private PolicyMembershipCondition? ReadMembership(
        string fact,
        PolicyOperator @operator,
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            _context.Error(path, "policy.json.type", "The JSON value has an invalid type.");
            return null;
        }

        int count = value.GetArrayLength();
        if (count is 0 or > PolicyContractLimits.MaximumMembershipValues)
        {
            _context.Error(
                path,
                "policy.limit.values",
                "Membership conditions require a bounded non-empty value list.");
        }

        List<PolicyValue> values = [];
        int index = 0;
        bool complete = count > 0 && count <= PolicyContractLimits.MaximumMembershipValues;
        foreach (JsonElement item in value.EnumerateArray())
        {
            PolicyValue? parsed = ReadValue(item, $"{path}[{index}]");
            complete &= parsed is not null;
            if (parsed is not null)
            {
                values.Add(parsed);
            }

            index++;
        }

        return complete ? new PolicyMembershipCondition(fact, @operator, values) : null;
    }

    private PolicyComparisonCondition? ReadComparison(
        string fact,
        PolicyOperator @operator,
        JsonElement value,
        string path)
    {
        PolicyValue? parsed = ReadValue(value, path);
        return parsed is null ? null : new PolicyComparisonCondition(fact, @operator, parsed);
    }

    private PolicyValue? ReadValue(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return new PolicyStringValue(value.GetString()!);
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return new PolicyBooleanValue(value.GetBoolean());
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal number))
        {
            return new PolicyNumberValue(number);
        }

        _context.Error(
            path,
            "policy.json.type",
            "Policy values must be strings, numbers or booleans.");
        return null;
    }
}
