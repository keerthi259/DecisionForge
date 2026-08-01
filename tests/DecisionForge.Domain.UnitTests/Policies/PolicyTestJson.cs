using System.Text.Json;

namespace DecisionForge.Domain.UnitTests.Policies;

internal static class PolicyTestJson
{
    public const string BooleanCondition =
        """
        {
          "fact": "supplier.isActive",
          "operator": "equals",
          "value": true
        }
        """;

    public static string Policy(
        string? rules = null,
        string? defaultOutcome = null,
        string schemaVersion = "1.0",
        string policyCode = "TEST-POLICY",
        string name = "Test policy")
    {
        return $$"""
        {
          "schemaVersion": {{JsonSerializer.Serialize(schemaVersion)}},
          "policyCode": {{JsonSerializer.Serialize(policyCode)}},
          "name": {{JsonSerializer.Serialize(name)}},
          "defaultOutcome": {{defaultOutcome ?? Outcome()}},
          "rules": [{{rules ?? Rule()}}]
        }
        """;
    }

    public static string Rule(
        string? condition = null,
        string? outcome = null,
        string id = "RULE-1",
        int priority = 1)
    {
        return $$"""
        {
          "id": {{JsonSerializer.Serialize(id)}},
          "priority": {{priority}},
          "when": {{condition ?? BooleanCondition}},
          "then": {{outcome ?? Outcome("Rejected", null, "RULE_MATCHED", "The rule matched.")}}
        }
        """;
    }

    public static string Outcome(
        string disposition = "AutoApproved",
        string? roles = null,
        string reasonCode = "DEFAULT_OUTCOME",
        string message = "The default outcome applies.")
    {
        string rolesProperty = roles is null
            ? string.Empty
            : $$"""
            "requiredApproverRoles": {{roles}},
            """;
        return $$"""
        {
          "disposition": {{JsonSerializer.Serialize(disposition)}},
          {{rolesProperty}}
          "reasonCode": {{JsonSerializer.Serialize(reasonCode)}},
          "message": {{JsonSerializer.Serialize(message)}}
        }
        """;
    }

    public static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Policies", "Fixtures", name);
    }

    public static string ReadFixture(string name)
    {
        return File.ReadAllText(FixturePath(name));
    }
}
