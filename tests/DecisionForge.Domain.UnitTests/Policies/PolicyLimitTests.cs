using System.Text;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyLimitTests
{
    [Fact]
    public void JsonSizeAcceptsExactLimitAndRejectsLimitPlusOne()
    {
        string compact = PolicyTestJson.Policy(rules: string.Empty);
        int baseSize = Encoding.UTF8.GetByteCount(compact);
        string exact = compact + new string(' ', PolicyContractLimits.MaximumJsonBytes - baseSize);
        string excessive = exact + " ";

        Assert.True(PolicyJsonParser.Parse(exact).IsValid);
        AssertError(excessive, "policy.limit.json-size");
    }

    [Fact]
    public void RuleCountAcceptsExactLimitAndRejectsLimitPlusOne()
    {
        string exact = Rules(PolicyContractLimits.MaximumRules);
        string excessive = Rules(PolicyContractLimits.MaximumRules + 1);

        Assert.True(PolicyJsonParser.Parse(PolicyTestJson.Policy(exact)).IsValid);
        AssertError(PolicyTestJson.Policy(excessive), "policy.limit.rules");
    }

    [Fact]
    public void ConditionDepthAcceptsExactLimitAndRejectsLimitPlusOne()
    {
        string exact = NestedNot(PolicyContractLimits.MaximumConditionDepth);
        string excessive = NestedNot(PolicyContractLimits.MaximumConditionDepth + 1);

        Assert.True(
            PolicyJsonParser.Parse(PolicyTestJson.Policy(PolicyTestJson.Rule(exact))).IsValid);
        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(excessive)),
            "policy.limit.condition-depth");
    }

    [Fact]
    public void LogicalChildrenAcceptExactLimitAndRejectLimitPlusOne()
    {
        string exact = LogicalChildren(PolicyContractLimits.MaximumConditionChildren);
        string excessive = LogicalChildren(PolicyContractLimits.MaximumConditionChildren + 1);

        Assert.True(
            PolicyJsonParser.Parse(PolicyTestJson.Policy(PolicyTestJson.Rule(exact))).IsValid);
        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(excessive)),
            "policy.limit.children");
    }

    [Fact]
    public void MembershipValuesAcceptExactLimitAndRejectLimitPlusOne()
    {
        string exact = Membership(PolicyContractLimits.MaximumMembershipValues);
        string excessive = Membership(PolicyContractLimits.MaximumMembershipValues + 1);

        Assert.True(
            PolicyJsonParser.Parse(PolicyTestJson.Policy(PolicyTestJson.Rule(exact))).IsValid);
        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(excessive)),
            "policy.limit.values");
    }

    [Fact]
    public void IdentifierAndMessageLengthsEnforceExactBoundaries()
    {
        string validRule = PolicyTestJson.Rule(
            outcome: PolicyTestJson.Outcome(
                reasonCode: new string('R', PolicyContractLimits.MaximumReasonCodeLength),
                message: new string('m', PolicyContractLimits.MaximumReasonMessageLength)),
            id: new string('I', PolicyContractLimits.MaximumRuleIdLength));
        Assert.True(PolicyJsonParser.Parse(PolicyTestJson.Policy(validRule)).IsValid);

        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(
                id: new string('I', PolicyContractLimits.MaximumRuleIdLength + 1))),
            "policy.value.format");
        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(
                outcome: PolicyTestJson.Outcome(
                    reasonCode: new string(
                        'R',
                        PolicyContractLimits.MaximumReasonCodeLength + 1)))),
            "policy.value.format");
        AssertError(
            PolicyTestJson.Policy(PolicyTestJson.Rule(
                outcome: PolicyTestJson.Outcome(
                    message: new string(
                        'm',
                        PolicyContractLimits.MaximumReasonMessageLength + 1)))),
            "policy.value.length");
    }

    private static string Rules(int count)
    {
        return string.Join(
            ',',
            Enumerable.Range(1, count).Select(index => PolicyTestJson.Rule(id: $"RULE-{index}")));
    }

    private static string NestedNot(int depth)
    {
        string result = PolicyTestJson.BooleanCondition;
        for (int current = 1; current < depth; current++)
        {
            result = $$"""{"not":{{result}}}""";
        }

        return result;
    }

    private static string LogicalChildren(int count)
    {
        string children = string.Join(
            ',',
            Enumerable.Repeat(PolicyTestJson.BooleanCondition, count));
        return $$"""{"all":[{{children}}]}""";
    }

    private static string Membership(int count)
    {
        string values = string.Join(',', Enumerable.Repeat("\"Urgent\"", count));
        return $$"""
        {"fact":"request.urgency","operator":"in","value":[{{values}}]}
        """;
    }

    private static void AssertError(string json, string code)
    {
        PolicyParseResult result = PolicyJsonParser.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == code);
    }
}
