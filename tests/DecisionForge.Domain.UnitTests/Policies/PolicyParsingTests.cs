using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyParsingTests
{
    [Fact]
    public void CompleteFixtureParsesIntoClosedImmutableAst()
    {
        PolicyParseResult result = PolicyJsonParser.Parse(
            PolicyTestJson.ReadFixture("valid-complete-policy.json"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        PolicyDefinition definition = Assert.IsType<PolicyDefinition>(result.Definition);
        Assert.Equal("1.0", definition.SchemaVersion);
        Assert.Equal("PROCUREMENT-GLOBAL", definition.PolicyCode);
        Assert.Equal(5, definition.Rules.Count);
        Assert.IsType<PolicyAllCondition>(definition.Rules[0].When);
        Assert.IsType<PolicyAnyCondition>(definition.Rules[1].When);
        Assert.IsType<PolicyNotCondition>(definition.Rules[2].When);
        Assert.IsType<PolicyExistenceCondition>(definition.Rules[3].When);
        Assert.IsType<PolicyComparisonCondition>(definition.Rules[4].When);
        Assert.Equal(DecisionDisposition.AutoApproved, definition.DefaultOutcome.Disposition);
    }

    [Theory]
    [InlineData("invalid-unknown-fact.json", "policy.fact.unknown")]
    [InlineData("invalid-unknown-property.json", "policy.json.unknown-property")]
    [InlineData("invalid-duplicate-rule.json", "policy.rule.duplicate-id")]
    [InlineData("invalid-value-type.json", "policy.value.type")]
    [InlineData("invalid-malformed.json", "policy.json.malformed")]
    public void InvalidFixturesReturnControlledErrors(string fixture, string expectedCode)
    {
        PolicyParseResult result = PolicyJsonParser.Parse(PolicyTestJson.ReadFixture(fixture));

        Assert.False(result.IsValid);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
        Assert.All(result.Errors, AssertNormalizedError);
    }

    [Theory]
    [InlineData(null, "policy.json.required")]
    [InlineData("", "policy.json.malformed")]
    [InlineData(" ", "policy.json.malformed")]
    [InlineData("[]", "policy.json.type")]
    [InlineData("{}", "policy.json.required")]
    [InlineData("{\"schemaVersion\":\"1.0\",}", "policy.json.malformed")]
    [InlineData("{/*comment*/}", "policy.json.malformed")]
    public void NullEmptyAndMalformedInputReturnsSafeError(string? json, string expectedCode)
    {
        PolicyParseResult result = PolicyJsonParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
        Assert.DoesNotContain(
            result.Errors,
            error => error.Message.Contains("JsonException", StringComparison.Ordinal)
                || error.Message.Contains("BytePositionInLine", StringComparison.Ordinal));
    }

    [Fact]
    public void ParsedCollectionsCannotBeMutatedThroughDowncasts()
    {
        PolicyDefinition definition = ValidDefinition();
        ICollection<PolicyRule> rules = Assert.IsAssignableFrom<ICollection<PolicyRule>>(
            definition.Rules);
        PolicyAllCondition all = Assert.IsType<PolicyAllCondition>(definition.Rules[0].When);
        ICollection<PolicyCondition> children =
            Assert.IsAssignableFrom<ICollection<PolicyCondition>>(all.Children);
        ICollection<PolicyApproverRole> roles =
            Assert.IsAssignableFrom<ICollection<PolicyApproverRole>>(
                definition.Rules[0].Then.RequiredApproverRoles);

        Assert.Throws<NotSupportedException>(rules.Clear);
        Assert.Throws<NotSupportedException>(children.Clear);
        Assert.Throws<NotSupportedException>(roles.Clear);
    }

    private static PolicyDefinition ValidDefinition()
    {
        string condition = $$"""
        {
          "all": [
            {{PolicyTestJson.BooleanCondition}}
          ]
        }
        """;
        string outcome = PolicyTestJson.Outcome(
            "ManualApprovalRequired",
            "[\"FinanceApprover\"]",
            "FINANCE",
            "Finance review is required.");
        PolicyParseResult result = PolicyJsonParser.Parse(
            PolicyTestJson.Policy(PolicyTestJson.Rule(condition, outcome)));
        return Assert.IsType<PolicyDefinition>(result.Definition);
    }

    private static void AssertNormalizedError(PolicyValidationError error)
    {
        Assert.StartsWith("$", error.Path, StringComparison.Ordinal);
        Assert.StartsWith("policy.", error.Code, StringComparison.Ordinal);
        Assert.Equal(PolicyValidationSeverity.Error, error.Severity);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.DoesNotContain("Exception", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
