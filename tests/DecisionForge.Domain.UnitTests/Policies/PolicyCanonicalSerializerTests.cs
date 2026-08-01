using System.Globalization;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyCanonicalSerializerTests
{
    private const string _expectedCanonical =
        "{\"schemaVersion\":\"1.0\",\"policyCode\":\"TEST-POLICY\",\"name\":\"Test policy\","
        + "\"defaultOutcome\":{\"disposition\":\"AutoApproved\",\"reasonCode\":\"DEFAULT_OUTCOME\","
        + "\"message\":\"The default outcome applies.\"},\"rules\":[{\"id\":\"RULE-1\",\"priority\":1,"
        + "\"when\":{\"fact\":\"request.totalAmount\",\"operator\":\"greaterThan\",\"value\":500000},"
        + "\"then\":{\"disposition\":\"Rejected\",\"reasonCode\":\"RULE_MATCHED\","
        + "\"message\":\"The rule matched.\"}}]}";

    [Fact]
    public void CanonicalSerializationHasStableGoldenShapeAndChecksum()
    {
        PolicyDefinition definition = Parse(NumericPolicy("500000.00"));

        string canonical = PolicyCanonicalSerializer.Serialize(definition);
        PolicyChecksum checksum = PolicyCanonicalSerializer.CalculateChecksum(definition);

        Assert.Equal(_expectedCanonical, canonical);
        Assert.Equal(
            "92ae5aa39babbd9f420a5b53c7e4cd70061fe1bc8d972a509dd17f1a41eb2e5b",
            checksum.Value);
    }

    [Fact]
    public void EquivalentSupportedJsonProducesSameCanonicalBytesAndChecksum()
    {
        PolicyDefinition first = Parse(NumericPolicy("500000.00"));
        string reordered =
            """
            {
              "rules": [{
                "then": {
                  "message": "The rule matched.",
                  "reasonCode": "rule_matched",
                  "disposition": "Rejected"
                },
                "when": {
                  "value": 5e5,
                  "operator": "greaterThan",
                  "fact": "request.totalAmount"
                },
                "priority": 1,
                "id": "rule-1"
              }],
              "defaultOutcome": {
                "requiredApproverRoles": [],
                "message": "The default outcome applies.",
                "reasonCode": "default_outcome",
                "disposition": "AutoApproved"
              },
              "name": " Test policy ",
              "policyCode": "test-policy",
              "schemaVersion": "1.0"
            }
            """;
        PolicyDefinition second = Parse(reordered);

        Assert.Equal(
            PolicyCanonicalSerializer.Serialize(first),
            PolicyCanonicalSerializer.Serialize(second));
        Assert.Equal(
            PolicyCanonicalSerializer.CalculateChecksum(first),
            PolicyCanonicalSerializer.CalculateChecksum(second));
    }

    [Fact]
    public void CanonicalRoundTripIsStableAndCultureIndependent()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            PolicyDefinition first = Parse(NumericPolicy("500000.25"));
            string canonical = PolicyCanonicalSerializer.Serialize(first);
            PolicyDefinition second = Parse(canonical);

            Assert.Contains("500000.25", canonical, StringComparison.Ordinal);
            Assert.Equal(canonical, PolicyCanonicalSerializer.Serialize(second));
            Assert.Equal(
                PolicyCanonicalSerializer.CalculateChecksum(first),
                PolicyCanonicalSerializer.CalculateChecksum(second));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void CompletePolicyCanonicalizesEverySupportedNodeAndValueShape()
    {
        PolicyDefinition first = Parse(
            PolicyTestJson.ReadFixture("valid-complete-policy.json"));

        string canonical = PolicyCanonicalSerializer.Serialize(first);
        PolicyDefinition second = Parse(canonical);

        Assert.Equal(canonical, PolicyCanonicalSerializer.Serialize(second));
        Assert.Contains(
            "\"requiredApproverRoles\":[\"FinanceApprover\",\"SeniorApprover\"]",
            canonical);
        Assert.Contains("\"all\":[", canonical);
        Assert.Contains("\"any\":[", canonical);
        Assert.Contains("\"not\":{", canonical);
        Assert.Contains("\"operator\":\"in\",\"value\":[", canonical);
        Assert.Contains("\"operator\":\"exists\"", canonical);
        Assert.Contains("\"value\":true", canonical);
        Assert.Equal(
            PolicyCanonicalSerializer.CalculateChecksum(first),
            PolicyCanonicalSerializer.CalculateChecksum(second));
    }

    [Fact]
    public void SerializerRejectsNullDefinition()
    {
        Assert.Throws<ArgumentNullException>(
            () => PolicyCanonicalSerializer.Serialize(null!));
        Assert.Throws<ArgumentNullException>(
            () => PolicyCanonicalSerializer.SerializeToUtf8Bytes(null!));
        Assert.Throws<ArgumentNullException>(
            () => PolicyCanonicalSerializer.CalculateChecksum(null!));
    }

    private static string NumericPolicy(string value)
    {
        string condition = $$"""
        {"fact":"request.totalAmount","operator":"greaterThan","value":{{value}}}
        """;
        return PolicyTestJson.Policy(PolicyTestJson.Rule(condition));
    }

    private static PolicyDefinition Parse(string json)
    {
        PolicyParseResult result = PolicyJsonParser.Parse(json);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        return Assert.IsType<PolicyDefinition>(result.Definition);
    }
}
