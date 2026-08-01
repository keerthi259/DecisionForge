using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.Domain.UnitTests.Policies;

internal static class PolicyEvaluationTestData
{
    public static PolicyDefinition GoldenPolicy()
    {
        return Parse(PolicyTestJson.ReadFixture("golden-procurement-policy.json"));
    }

    public static PolicyDefinition Parse(string json)
    {
        PolicyParseResult parsed = PolicyJsonParser.Parse(json);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Errors));
        return Assert.IsType<PolicyDefinition>(parsed.Definition);
    }

    public static PolicyFactSet Facts(
        decimal totalAmount = 10_000m,
        bool hasJustification = true,
        string onboardingStatus = "Completed",
        string riskRating = "Low",
        bool supplierApproved = true,
        bool containsTechnology = false,
        string dataSensitivity = "Internal",
        string urgency = "Normal")
    {
        return PolicyFactSet.Create(
        [
            PolicyFact.DecimalNumber("request.totalAmount", totalAmount),
            PolicyFact.Logical(
                "request.hasBusinessJustification",
                hasJustification),
            PolicyFact.ControlledText(
                "supplier.onboardingStatus",
                onboardingStatus),
            PolicyFact.ControlledText("supplier.riskRating", riskRating),
            PolicyFact.Logical("supplier.isApproved", supplierApproved),
            PolicyFact.Logical(
                "derived.containsTechnologyPurchase",
                containsTechnology),
            PolicyFact.ControlledText(
                "request.dataSensitivity",
                dataSensitivity),
            PolicyFact.ControlledText("request.urgency", urgency),
        ]);
    }

    public static PolicyDefinition SingleRule(
        string fact,
        string @operator,
        string? value,
        string disposition = "Rejected",
        string? roles = null,
        string reasonCode = "MATCHED",
        string message = "The rule matched.")
    {
        string valueProperty = value is null ? string.Empty : $",\"value\":{value}";
        string condition = $$"""
        {"fact":"{{fact}}","operator":"{{@operator}}"{{valueProperty}}}
        """;
        string outcome = PolicyTestJson.Outcome(
            disposition,
            roles,
            reasonCode,
            message);
        return Parse(PolicyTestJson.Policy(PolicyTestJson.Rule(condition, outcome)));
    }
}
