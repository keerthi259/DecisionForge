using System.Collections.ObjectModel;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Facts;

namespace DecisionForge.Domain.Policies.Validation;

public static class PolicyValidator
{
    public static IReadOnlyList<PolicyValidationError> Validate(PolicyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        List<PolicyValidationError> errors = [];

        if (!string.Equals(
                definition.SchemaVersion,
                PolicyContractLimits.SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            Add(
                errors,
                "$.schemaVersion",
                "policy.schema.unsupported",
                "The policy schema version is not supported.");
        }

        if (definition.Rules.Count > PolicyContractLimits.MaximumRules)
        {
            Add(errors, "$.rules", "policy.limit.rules", "The policy contains too many rules.");
        }

        Dictionary<string, string> reasons = new(StringComparer.Ordinal);
        ValidateOutcome(definition.DefaultOutcome, "$.defaultOutcome", reasons, errors);

        HashSet<string> ruleIds = new(StringComparer.Ordinal);
        for (int index = 0; index < definition.Rules.Count; index++)
        {
            PolicyRule rule = definition.Rules[index];
            string path = $"$.rules[{index}]";
            if (!ruleIds.Add(rule.Id))
            {
                Add(
                    errors,
                    $"{path}.id",
                    "policy.rule.duplicate-id",
                    "Rule identifiers must be unique.");
            }

            if (rule.Priority < 0)
            {
                Add(
                    errors,
                    $"{path}.priority",
                    "policy.rule.priority",
                    "Rule priority must not be negative.");
            }

            ValidateCondition(rule.When, $"{path}.when", 1, errors);
            ValidateOutcome(rule.Then, $"{path}.then", reasons, errors);
        }

        return new ReadOnlyCollection<PolicyValidationError>(errors);
    }

    private static void ValidateCondition(
        PolicyCondition condition,
        string path,
        int depth,
        ICollection<PolicyValidationError> errors)
    {
        if (depth > PolicyContractLimits.MaximumConditionDepth)
        {
            Add(
                errors,
                path,
                "policy.limit.condition-depth",
                "The condition tree exceeds the supported depth.");
            return;
        }

        switch (condition)
        {
            case PolicyComparisonCondition comparison:
                ValidateLeaf(
                    comparison.Fact,
                    comparison.Operator,
                    [comparison.Value],
                    $"{path}.value",
                    path,
                    errors);
                break;
            case PolicyMembershipCondition membership:
                if (membership.Values.Count is 0 or > PolicyContractLimits.MaximumMembershipValues)
                {
                    Add(
                        errors,
                        $"{path}.value",
                        "policy.limit.values",
                        "Membership conditions require a bounded non-empty value list.");
                }

                ValidateLeaf(
                    membership.Fact,
                    membership.Operator,
                    membership.Values,
                    $"{path}.value",
                    path,
                    errors);
                break;
            case PolicyExistenceCondition existence:
                ValidateLeaf(
                    existence.Fact,
                    existence.Operator,
                    [],
                    $"{path}.value",
                    path,
                    errors);
                break;
            case PolicyAllCondition all:
                ValidateChildren(all.Children, $"{path}.all", depth, errors);
                break;
            case PolicyAnyCondition any:
                ValidateChildren(any.Children, $"{path}.any", depth, errors);
                break;
            case PolicyNotCondition not:
                ValidateCondition(not.Child, $"{path}.not", depth + 1, errors);
                break;
            default:
                Add(
                    errors,
                    path,
                    "policy.condition.unsupported",
                    "The condition node is not supported.");
                break;
        }
    }

    private static void ValidateChildren(
        IReadOnlyList<PolicyCondition> children,
        string path,
        int depth,
        ICollection<PolicyValidationError> errors)
    {
        if (children.Count is 0 or > PolicyContractLimits.MaximumConditionChildren)
        {
            Add(
                errors,
                path,
                "policy.limit.children",
                "Logical conditions require a bounded non-empty child list.");
        }

        for (int index = 0; index < children.Count; index++)
        {
            ValidateCondition(children[index], $"{path}[{index}]", depth + 1, errors);
        }
    }

    private static void ValidateLeaf(
        string fact,
        PolicyOperator @operator,
        IReadOnlyList<PolicyValue> values,
        string valuePath,
        string conditionPath,
        ICollection<PolicyValidationError> errors)
    {
        if (!PolicyFactRegistry.TryGet(fact, out PolicyFactMetadata metadata))
        {
            Add(
                errors,
                $"{conditionPath}.fact",
                "policy.fact.unknown",
                "The condition references an unsupported fact path.");
            return;
        }

        if (!metadata.AllowedOperators.Contains(@operator))
        {
            Add(
                errors,
                $"{conditionPath}.operator",
                "policy.operator.not-allowed",
                "The operator is not allowed for this fact type.");
        }

        for (int index = 0; index < values.Count; index++)
        {
            string path = values.Count == 1 ? valuePath : $"{valuePath}[{index}]";
            ValidateValue(values[index], metadata, path, errors);
        }
    }

    private static void ValidateValue(
        PolicyValue value,
        PolicyFactMetadata metadata,
        string path,
        ICollection<PolicyValidationError> errors)
    {
        bool matches = metadata.ValueType switch
        {
            PolicyFactValueType.DecimalNumber => value is PolicyNumberValue,
            PolicyFactValueType.Text => value is PolicyStringValue,
            PolicyFactValueType.Logical => value is PolicyBooleanValue,
            PolicyFactValueType.WholeNumber => IsInteger(value),
            PolicyFactValueType.ControlledText => IsAllowedEnum(value, metadata.AllowedValues),
            _ => false,
        };

        if (!matches)
        {
            Add(
                errors,
                path,
                "policy.value.type",
                "The comparison value does not match the fact type.");
        }
    }

    private static bool IsInteger(PolicyValue value)
    {
        return value is PolicyNumberValue number
            && decimal.Truncate(number.Value) == number.Value
            && number.Value is >= int.MinValue and <= int.MaxValue;
    }

    private static bool IsAllowedEnum(
        PolicyValue value,
        IReadOnlyList<string> allowedValues)
    {
        return value is PolicyStringValue text
            && allowedValues.Contains(text.Value, StringComparer.Ordinal);
    }

    private static void ValidateOutcome(
        PolicyOutcome outcome,
        string path,
        Dictionary<string, string> reasons,
        ICollection<PolicyValidationError> errors)
    {
        bool isManual = outcome.Disposition == DecisionDisposition.ManualApprovalRequired;
        if (isManual && outcome.RequiredApproverRoles.Count == 0)
        {
            Add(
                errors,
                $"{path}.requiredApproverRoles",
                "policy.outcome.roles-required",
                "Manual approval requires at least one approver role.");
        }
        else if (!isManual && outcome.RequiredApproverRoles.Count > 0)
        {
            Add(
                errors,
                $"{path}.requiredApproverRoles",
                "policy.outcome.roles-forbidden",
                "Only manual-approval outcomes may require approver roles.");
        }

        if (outcome.RequiredApproverRoles.Distinct().Count()
            != outcome.RequiredApproverRoles.Count)
        {
            Add(
                errors,
                $"{path}.requiredApproverRoles",
                "policy.outcome.duplicate-role",
                "Required approver roles must be unique within an outcome.");
        }

        string reasonCode = outcome.ReasonCode.Value;
        if (reasons.TryGetValue(reasonCode, out string? message)
            && !string.Equals(message, outcome.Message, StringComparison.Ordinal))
        {
            Add(
                errors,
                $"{path}.reasonCode",
                "policy.reason.conflict",
                "A reason code must map to one consistent message.");
        }
        else
        {
            reasons[reasonCode] = outcome.Message;
        }
    }

    private static void Add(
        ICollection<PolicyValidationError> errors,
        string path,
        string code,
        string message)
    {
        errors.Add(new PolicyValidationError(
            path,
            code,
            PolicyValidationSeverity.Error,
            message));
    }
}
