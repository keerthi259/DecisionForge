using System.Text.Json;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Parsing;

internal sealed class PolicyDocumentReader
{
    private readonly PolicyConditionReader _conditions;
    private readonly PolicyJsonReaderContext _context = new();

    public PolicyDocumentReader()
    {
        _conditions = new PolicyConditionReader(_context);
    }

    public IReadOnlyList<PolicyValidationError> Errors => _context.Errors;

    public PolicyDefinition? Read(JsonElement root)
    {
        Dictionary<string, JsonElement>? properties = _context.ReadObject(
            root,
            "$",
            ["schemaVersion", "policyCode", "name", "defaultOutcome", "rules"],
            ["schemaVersion", "policyCode", "name", "defaultOutcome", "rules"]);
        if (properties is null)
        {
            return null;
        }

        string? schemaVersion = _context.ReadString(properties, "schemaVersion", "$");
        string? policyCode = _context.ReadCode(
            properties,
            "policyCode",
            "$",
            PolicyContractLimits.MaximumPolicyCodeLength);
        string? name = _context.ReadRequiredText(
            properties,
            "name",
            "$",
            PolicyContractLimits.MaximumPolicyNameLength);
        PolicyOutcome? defaultOutcome = ReadOutcome(
            PolicyJsonReaderContext.Get(properties, "defaultOutcome"),
            "$.defaultOutcome");
        List<PolicyRule>? rules = ReadRules(
            PolicyJsonReaderContext.Get(properties, "rules"),
            "$.rules");

        return schemaVersion is null
            || policyCode is null
            || name is null
            || defaultOutcome is null
            || rules is null
                ? null
                : new PolicyDefinition(schemaVersion, policyCode, name, defaultOutcome, rules);
    }

    private List<PolicyRule>? ReadRules(JsonElement? element, string path)
    {
        if (!_context.RequireKind(element, JsonValueKind.Array, path))
        {
            return null;
        }

        int count = element!.Value.GetArrayLength();
        if (count > PolicyContractLimits.MaximumRules)
        {
            _context.Error(path, "policy.limit.rules", "The policy contains too many rules.");
        }

        List<PolicyRule> rules = [];
        int index = 0;
        bool complete = count <= PolicyContractLimits.MaximumRules;
        foreach (JsonElement value in element.Value.EnumerateArray())
        {
            PolicyRule? rule = ReadRule(value, $"{path}[{index}]");
            complete &= rule is not null;
            if (rule is not null)
            {
                rules.Add(rule);
            }

            index++;
        }

        return complete ? rules : null;
    }

    private PolicyRule? ReadRule(JsonElement element, string path)
    {
        Dictionary<string, JsonElement>? properties = _context.ReadObject(
            element,
            path,
            ["id", "priority", "when", "then"],
            ["id", "priority", "when", "then"]);
        if (properties is null)
        {
            return null;
        }

        string? id = _context.ReadCode(
            properties,
            "id",
            path,
            PolicyContractLimits.MaximumRuleIdLength);
        int? priority = _context.ReadInteger(properties, "priority", path);
        PolicyCondition? when = _conditions.Read(
            PolicyJsonReaderContext.Get(properties, "when"),
            $"{path}.when",
            1);
        PolicyOutcome? then = ReadOutcome(
            PolicyJsonReaderContext.Get(properties, "then"),
            $"{path}.then");

        return id is null || priority is null || when is null || then is null
            ? null
            : new PolicyRule(id, priority.Value, when, then);
    }

    private PolicyOutcome? ReadOutcome(JsonElement? element, string path)
    {
        Dictionary<string, JsonElement>? properties = _context.ReadObject(
            element,
            path,
            ["disposition", "requiredApproverRoles", "reasonCode", "message"],
            ["disposition", "reasonCode", "message"]);
        if (properties is null)
        {
            return null;
        }

        DecisionDisposition? disposition = _context.ReadControlledEnum<DecisionDisposition>(
            properties,
            "disposition",
            path,
            "policy.disposition.unknown",
            "The outcome disposition is not supported.");
        List<PolicyApproverRole>? roles = properties.TryGetValue(
            "requiredApproverRoles",
            out JsonElement rolesElement)
                ? ReadRoles(rolesElement, $"{path}.requiredApproverRoles")
                : [];
        ReasonCode? reasonCode = _context.ReadReasonCode(properties, "reasonCode", path);
        string? message = _context.ReadRequiredText(
            properties,
            "message",
            path,
            PolicyContractLimits.MaximumReasonMessageLength);

        return disposition is null || roles is null || reasonCode is null || message is null
            ? null
            : new PolicyOutcome(disposition.Value, roles, reasonCode, message);
    }

    private List<PolicyApproverRole>? ReadRoles(JsonElement element, string path)
    {
        if (!_context.RequireKind(element, JsonValueKind.Array, path))
        {
            return null;
        }

        List<PolicyApproverRole> roles = [];
        int index = 0;
        bool complete = true;
        foreach (JsonElement value in element.EnumerateArray())
        {
            string itemPath = $"{path}[{index}]";
            if (value.ValueKind != JsonValueKind.String
                || !ControlledEnumParser.TryParse(value.GetString(), out PolicyApproverRole role))
            {
                _context.Error(
                    itemPath,
                    "policy.role.unknown",
                    "The approver role is not supported.");
                complete = false;
            }
            else
            {
                roles.Add(role);
            }

            index++;
        }

        return complete ? roles : null;
    }
}
