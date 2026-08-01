using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyStructuralValidationTests
{
    [Fact]
    public void UnknownPropertiesAreRejectedAtEveryObjectLevel()
    {
        string root = PolicyTestJson.Policy().Replace(
            "\"rules\":",
            "\"unexpected\": true, \"rules\":",
            StringComparison.Ordinal);
        string rule = PolicyTestJson.Policy(
            PolicyTestJson.Rule().Replace(
                "\"priority\":",
                "\"script\": \"never\", \"priority\":",
                StringComparison.Ordinal));
        string condition = PolicyTestJson.Policy(PolicyTestJson.Rule(
            PolicyTestJson.BooleanCondition.Replace(
                "\"value\":",
                "\"method\": \"execute\", \"value\":",
                StringComparison.Ordinal)));
        string outcome = PolicyTestJson.Policy(
            defaultOutcome: PolicyTestJson.Outcome().Replace(
                "\"message\":",
                "\"debug\": true, \"message\":",
                StringComparison.Ordinal));

        AssertError(root, "policy.json.unknown-property");
        AssertError(rule, "policy.json.unknown-property");
        AssertError(condition, "policy.json.unknown-property");
        AssertError(outcome, "policy.json.unknown-property");
    }

    [Fact]
    public void DuplicateJsonPropertiesAreRejected()
    {
        string duplicate = PolicyTestJson.Policy().Replace(
            "\"schemaVersion\": \"1.0\",",
            "\"schemaVersion\": \"1.0\", \"schemaVersion\": \"1.0\",",
            StringComparison.Ordinal);

        AssertError(duplicate, "policy.json.duplicate-property");
    }

    [Theory]
    [InlineData("Unknown", null, "policy.disposition.unknown")]
    [InlineData("ManualApprovalRequired", "[\"Requester\"]", "policy.role.unknown")]
    [InlineData("ManualApprovalRequired", "[1]", "policy.role.unknown")]
    public void UnknownDispositionAndRolesAreRejected(
        string disposition,
        string? roles,
        string expectedCode)
    {
        string json = PolicyTestJson.Policy(
            defaultOutcome: PolicyTestJson.Outcome(disposition, roles));

        AssertError(json, expectedCode);
    }

    [Theory]
    [InlineData("Equal", "policy.operator.unknown")]
    [InlineData("EQUALS", "policy.operator.unknown")]
    [InlineData("", "policy.operator.unknown")]
    public void UnknownOperatorIsRejected(string @operator, string expectedCode)
    {
        string condition = $$"""
        {
          "fact": "supplier.isActive",
          "operator": "{{@operator}}",
          "value": true
        }
        """;

        AssertError(PolicyTestJson.Policy(PolicyTestJson.Rule(condition)), expectedCode);
    }

    [Theory]
    [MemberData(nameof(InvalidConditionShapes))]
    public void InvalidConditionNodeCombinationsAreRejected(string condition, string expectedCode)
    {
        AssertError(PolicyTestJson.Policy(PolicyTestJson.Rule(condition)), expectedCode);
    }

    public static TheoryData<string, string> InvalidConditionShapes => new()
    {
        {
            $$"""{"all":[{{PolicyTestJson.BooleanCondition}}],"fact":"supplier.isActive"}""",
            "policy.condition.shape"
        },
        { "{}", "policy.condition.shape" },
        { "{\"all\":[]}", "policy.limit.children" },
        { "{\"any\":true}", "policy.json.type" },
        {
            $$"""{"not":{{PolicyTestJson.BooleanCondition}},"any":[{{PolicyTestJson.BooleanCondition}}]}""",
            "policy.condition.shape"
        },
        {
            "{\"fact\":\"supplier.isActive\",\"operator\":\"exists\",\"value\":true}",
            "policy.condition.shape"
        },
        {
            "{\"fact\":\"supplier.isActive\",\"operator\":\"equals\"}",
            "policy.json.required"
        },
        {
            "{\"fact\":\"request.urgency\",\"operator\":\"in\",\"value\":\"Urgent\"}",
            "policy.json.type"
        },
        {
            "{\"fact\":\"request.urgency\",\"operator\":\"in\",\"value\":[]}",
            "policy.limit.values"
        },
        {
            "{\"operator\":\"equals\",\"value\":true}",
            "policy.json.required"
        },
    };

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void NonScalarComparisonValuesAreRejected(string value)
    {
        string condition = $$"""
        {
          "fact": "supplier.isActive",
          "operator": "equals",
          "value": {{value}}
        }
        """;

        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(condition)),
            "policy.json.type");
    }

    [Theory]
    [MemberData(nameof(InvalidPrimitiveValues))]
    public void InvalidPrimitiveTypesAndFormatsReturnNormalizedErrors(
        string json,
        string expectedCode)
    {
        AssertError(json, expectedCode);
    }

    public static TheoryData<string, string> InvalidPrimitiveValues
    {
        get
        {
            string valid = PolicyTestJson.Policy(PolicyTestJson.Rule());
            return new TheoryData<string, string>
            {
                {
                    valid.Replace("\"schemaVersion\": \"1.0\"", "\"schemaVersion\": 1", StringComparison.Ordinal),
                    "policy.json.type"
                },
                {
                    valid.Replace("\"policyCode\": \"TEST-POLICY\"", "\"policyCode\": \"\"", StringComparison.Ordinal),
                    "policy.value.format"
                },
                {
                    valid.Replace("\"name\": \"Test policy\"", "\"name\": \"\"", StringComparison.Ordinal),
                    "policy.value.length"
                },
                {
                    valid.Replace("\"priority\": 1", "\"priority\": 2147483648", StringComparison.Ordinal),
                    "policy.json.type"
                },
                {
                    valid.Replace("\"reasonCode\": \"DEFAULT_OUTCOME\"", "\"reasonCode\": \"\"", StringComparison.Ordinal),
                    "policy.value.format"
                },
                {
                    valid.Replace("\"rules\":", "\"unsafe.property\": true, \"rules\":", StringComparison.Ordinal),
                    "policy.json.unknown-property"
                },
            };
        }
    }

    private static void AssertError(string json, string expectedCode)
    {
        PolicyParseResult result = PolicyJsonParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Null(result.Definition);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }
}
