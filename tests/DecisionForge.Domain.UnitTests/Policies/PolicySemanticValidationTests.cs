using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicySemanticValidationTests
{
    [Theory]
    [InlineData("request.totalAmount", "greaterThan", "10")]
    [InlineData("request.currency", "contains", "\"INR\"")]
    [InlineData("request.category", "equals", "\"Hardware\"")]
    [InlineData("request.urgency", "notEquals", "\"Normal\"")]
    [InlineData("request.dataSensitivity", "in", "[\"Internal\",\"Restricted\"]")]
    [InlineData("request.itemCount", "greaterThanOrEqual", "1")]
    [InlineData("request.expectedDeliveryDays", "lessThan", "30")]
    [InlineData("request.hasBusinessJustification", "equals", "true")]
    [InlineData("department.code", "equals", "\"ENG\"")]
    [InlineData("department.autoApprovalLimit", "lessThanOrEqual", "250000")]
    [InlineData("supplier.isApproved", "notEquals", "false")]
    [InlineData("supplier.onboardingStatus", "equals", "\"Completed\"")]
    [InlineData("supplier.riskRating", "notIn", "[\"High\",\"Critical\"]")]
    [InlineData("supplier.isActive", "exists", null)]
    [InlineData("derived.containsTechnologyPurchase", "equals", "true")]
    [InlineData("derived.requiresUrgencyException", "notExists", null)]
    public void EveryApprovedFactAcceptsATypeCorrectOperator(
        string fact,
        string @operator,
        string? value)
    {
        string valueProperty = value is null ? string.Empty : $",\"value\":{value}";
        string condition = $$"""
        {"fact":"{{fact}}","operator":"{{@operator}}"{{valueProperty}}}
        """;

        PolicyParseResult result = PolicyJsonParser.Parse(
            PolicyTestJson.Policy(PolicyTestJson.Rule(condition)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData("equals", "true")]
    [InlineData("notEquals", "false")]
    [InlineData("greaterThan", "10")]
    [InlineData("greaterThanOrEqual", "10")]
    [InlineData("lessThan", "10")]
    [InlineData("lessThanOrEqual", "10")]
    [InlineData("in", "[10,20]")]
    [InlineData("notIn", "[10,20]")]
    [InlineData("exists", null)]
    [InlineData("notExists", null)]
    [InlineData("contains", "\"IN\"")]
    public void EveryOperatorHasAValidContractShape(string @operator, string? value)
    {
        bool stringOperator = @operator == "contains";
        string fact = stringOperator ? "request.currency" : "request.totalAmount";
        if (@operator is "equals" or "notEquals" && value is "true" or "false")
        {
            fact = "supplier.isActive";
        }

        string valueProperty = value is null ? string.Empty : $",\"value\":{value}";
        string condition = $$"""
        {"fact":"{{fact}}","operator":"{{@operator}}"{{valueProperty}}}
        """;

        PolicyParseResult result = PolicyJsonParser.Parse(
            PolicyTestJson.Policy(PolicyTestJson.Rule(condition)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData("request.totalAmount", "equals", "\"10\"")]
    [InlineData("request.itemCount", "equals", "1.5")]
    [InlineData("request.itemCount", "equals", "2147483648")]
    [InlineData("supplier.isActive", "equals", "1")]
    [InlineData("request.category", "equals", "\"hardware\"")]
    [InlineData("request.category", "equals", "\"Unknown\"")]
    public void FactTypeMismatchesAreRejected(string fact, string @operator, string value)
    {
        string condition = $$"""
        {"fact":"{{fact}}","operator":"{{@operator}}","value":{{value}}}
        """;

        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(condition)),
            "policy.value.type");
    }

    [Theory]
    [InlineData("supplier.isActive", "greaterThan", "true")]
    [InlineData("request.category", "contains", "\"Hard\"")]
    [InlineData("request.currency", "greaterThan", "\"INR\"")]
    public void OperatorsNotAllowedForFactTypeAreRejected(
        string fact,
        string @operator,
        string value)
    {
        string condition = $$"""
        {"fact":"{{fact}}","operator":"{{@operator}}","value":{{value}}}
        """;

        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(condition)),
            "policy.operator.not-allowed");
    }

    [Fact]
    public void OutcomeRoleRulesAreEnforced()
    {
        string manualWithoutRole = PolicyTestJson.Policy(
            defaultOutcome: PolicyTestJson.Outcome("ManualApprovalRequired"));
        string automaticWithRole = PolicyTestJson.Policy(
            defaultOutcome: PolicyTestJson.Outcome("AutoApproved", "[\"FinanceApprover\"]"));
        string duplicateRole = PolicyTestJson.Policy(
            defaultOutcome: PolicyTestJson.Outcome(
                "ManualApprovalRequired",
                "[\"FinanceApprover\",\"FinanceApprover\"]"));

        AssertError(manualWithoutRole, "policy.outcome.roles-required");
        AssertError(automaticWithRole, "policy.outcome.roles-forbidden");
        AssertError(duplicateRole, "policy.outcome.duplicate-role");
    }

    [Fact]
    public void SchemaPriorityAndReasonConsistencyAreEnforced()
    {
        string unsupportedSchema = PolicyTestJson.Policy(schemaVersion: "2.0");
        string negativePriority = PolicyTestJson.Policy(
            PolicyTestJson.Rule(priority: -1));
        string conflictingReason = PolicyTestJson.Policy(
            PolicyTestJson.Rule(
                outcome: PolicyTestJson.Outcome(
                    "Rejected",
                    null,
                    "DEFAULT_OUTCOME",
                    "A different meaning.")));

        AssertError(unsupportedSchema, "policy.schema.unsupported");
        AssertError(negativePriority, "policy.rule.priority");
        AssertError(conflictingReason, "policy.reason.conflict");
    }

    private static void AssertError(string json, string expectedCode)
    {
        PolicyParseResult result = PolicyJsonParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }
}
