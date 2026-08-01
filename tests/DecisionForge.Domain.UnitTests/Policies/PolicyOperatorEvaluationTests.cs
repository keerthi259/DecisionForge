using System.Globalization;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyOperatorEvaluationTests
{
    [Theory]
    [InlineData("equals", "10", 10, true)]
    [InlineData("equals", "10", 9, false)]
    [InlineData("notEquals", "10", 9, true)]
    [InlineData("notEquals", "10", 10, false)]
    [InlineData("greaterThan", "10", 11, true)]
    [InlineData("greaterThan", "10", 10, false)]
    [InlineData("greaterThanOrEqual", "10", 10, true)]
    [InlineData("greaterThanOrEqual", "10", 9, false)]
    [InlineData("lessThan", "10", 9, true)]
    [InlineData("lessThan", "10", 10, false)]
    [InlineData("lessThanOrEqual", "10", 10, true)]
    [InlineData("lessThanOrEqual", "10", 11, false)]
    public void NumericOperatorsRespectExactThresholds(
        string @operator,
        string expectedValue,
        int actualValue,
        bool expectedMatch)
    {
        PolicyEvaluationResult result = Evaluate(
            "request.totalAmount",
            @operator,
            expectedValue,
            PolicyFact.DecimalNumber("request.totalAmount", actualValue));

        Assert.Equal(expectedMatch, result.Rules[0].Matched);
        Assert.Equal(
            actualValue.ToString(CultureInfo.InvariantCulture),
            result.Rules[0].Condition.FactAccesses[0].Value);
    }

    [Theory]
    [InlineData("in", "[\"INR\",\"USD\"]", "INR", true)]
    [InlineData("in", "[\"INR\",\"USD\"]", "EUR", false)]
    [InlineData("notIn", "[\"INR\",\"USD\"]", "EUR", true)]
    [InlineData("notIn", "[\"INR\",\"USD\"]", "INR", false)]
    [InlineData("contains", "\"IN\"", "INR", true)]
    [InlineData("contains", "\"in\"", "INR", false)]
    [InlineData("equals", "\"INR\"", "INR", true)]
    [InlineData("notEquals", "\"USD\"", "INR", true)]
    public void TextAndMembershipOperatorsUseOrdinalSemantics(
        string @operator,
        string expectedValue,
        string actualValue,
        bool expectedMatch)
    {
        PolicyEvaluationResult result = Evaluate(
            "request.currency",
            @operator,
            expectedValue,
            PolicyFact.Text("request.currency", actualValue));

        Assert.Equal(expectedMatch, result.Rules[0].Matched);
    }

    [Theory]
    [InlineData("equals", "true", true, true)]
    [InlineData("equals", "true", false, false)]
    [InlineData("notEquals", "false", true, true)]
    [InlineData("notEquals", "false", false, false)]
    public void LogicalEqualityIsTyped(
        string @operator,
        string expectedValue,
        bool actualValue,
        bool expectedMatch)
    {
        PolicyEvaluationResult result = Evaluate(
            "supplier.isActive",
            @operator,
            expectedValue,
            PolicyFact.Logical("supplier.isActive", actualValue));

        Assert.Equal(expectedMatch, result.Rules[0].Matched);
    }

    [Theory]
    [InlineData("exists", true)]
    [InlineData("notExists", false)]
    public void ExistenceOperatorsObservePresence(string @operator, bool expectedMatch)
    {
        PolicyEvaluationResult result = Evaluate(
            "request.currency",
            @operator,
            null,
            PolicyFact.Text("request.currency", "INR"));

        Assert.Equal(expectedMatch, result.Rules[0].Matched);
        Assert.True(result.Rules[0].Condition.FactAccesses[0].Exists);
    }

    [Fact]
    public void WholeNumberComparisonUsesExactIntegerValue()
    {
        PolicyEvaluationResult result = Evaluate(
            "request.itemCount",
            "greaterThanOrEqual",
            "30",
            PolicyFact.WholeNumber("request.itemCount", 30));

        Assert.True(result.Rules[0].Matched);
        Assert.Equal(PolicyFactValueType.WholeNumber, result.Rules[0].Condition.FactAccesses[0].ValueType);
    }

    [Fact]
    public void MembershipRemainsTrueWhenTheExpectedListContainsDuplicateMatches()
    {
        PolicyEvaluationResult result = Evaluate(
            "request.currency",
            "in",
            "[\"INR\",\"INR\"]",
            PolicyFact.Text("request.currency", "INR"));

        Assert.True(result.Rules[0].Matched);
    }

    private static PolicyEvaluationResult Evaluate(
        string path,
        string @operator,
        string? value,
        PolicyFact fact)
    {
        return PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.SingleRule(path, @operator, value),
            PolicyFactSet.Create([fact]));
    }
}
