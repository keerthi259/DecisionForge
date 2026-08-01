using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyEvaluationLimitTests
{
    [Fact]
    public void PreCancelledEvaluationPropagatesCancellation()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() => PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.GoldenPolicy(),
            PolicyEvaluationTestData.Facts(),
            source.Token));
    }

    [Fact]
    public void ExactHundredRulePolicyEvaluatesEveryRule()
    {
        string rules = string.Join(
            ',',
            Enumerable.Range(1, 100).Select(index => PolicyTestJson.Rule(
                condition:
                    $$"""{"fact":"request.totalAmount","operator":"greaterThan","value":{{index}}}""",
                outcome: PolicyTestJson.Outcome(
                    "AutoApproved",
                    null,
                    "MATCHED",
                    "The rule matched."),
                id: $"RULE-{index:D3}",
                priority: 101 - index)));

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(rules)),
            PolicyFactSet.Create(
                [PolicyFact.DecimalNumber("request.totalAmount", 1_000m)]));

        Assert.Equal(100, result.Rules.Count);
        Assert.All(result.Rules, rule => Assert.True(rule.Matched));
        Assert.Equal("RULE-100", result.Rules[0].RuleId);
        Assert.Equal("RULE-001", result.Rules[^1].RuleId);
    }

    [Fact]
    public void ConditionExecutionLimitAcceptsExactBoundaryAndRejectsNextLogicalRule()
    {
        string exactJson = PolicyTestJson.Policy(ConditionLimitRules(logicalRuleCount: 96));
        string excessiveJson = PolicyTestJson.Policy(ConditionLimitRules(logicalRuleCount: 97));
        PolicyFactSet facts = PolicyFactSet.Create(
            [PolicyFact.Logical("supplier.isActive", true)]);

        PolicyEvaluationResult exact = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(exactJson),
            facts);
        PolicyEvaluationException excessive = Assert.Throws<PolicyEvaluationException>(
            () => PolicyEvaluator.Evaluate(
                PolicyEvaluationTestData.Parse(excessiveJson),
                facts));

        Assert.Equal(100, exact.Rules.Count);
        Assert.Equal(PolicyEvaluationLimits.MaximumConditionEvaluations, CountNodes(exact));
        Assert.Equal(PolicyEvaluationErrorCodes.ExecutionLimit, excessive.Code);
        Assert.Equal("$", excessive.Path);
        Assert.Equal(
            "Policy evaluation exceeded a configured execution limit.",
            excessive.Message);
    }

    [Fact]
    public void ExactMaximumConditionDepthEvaluatesSuccessfully()
    {
        string condition = PolicyTestJson.BooleanCondition;
        for (int depth = 1; depth < PolicyContractLimits.MaximumConditionDepth; depth++)
        {
            condition = $$"""{"not":{{condition}}}""";
        }

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(PolicyTestJson.Rule(condition))),
            PolicyFactSet.Create([PolicyFact.Logical("supplier.isActive", true)]));

        Assert.Equal(PolicyContractLimits.MaximumConditionDepth, CountNodes(result));
    }

    [Fact]
    public void NullPolicyAndFactSetAreRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => PolicyEvaluator.Evaluate(null!, PolicyEvaluationTestData.Facts()));
        Assert.Throws<ArgumentNullException>(
            () => PolicyEvaluator.Evaluate(PolicyEvaluationTestData.GoldenPolicy(), null!));
    }

    private static string ConditionLimitRules(int logicalRuleCount)
    {
        string children = string.Join(
            ',',
            Enumerable.Repeat(PolicyTestJson.BooleanCondition, 25));
        string logical = $$"""{"all":[{{children}}]}""";
        IEnumerable<string> logicalRules = Enumerable.Range(1, logicalRuleCount)
            .Select(index => PolicyTestJson.Rule(logical, id: $"LOGICAL-{index:D3}"));
        IEnumerable<string> leafRules = Enumerable.Range(
                logicalRuleCount + 1,
                100 - logicalRuleCount)
            .Select(index => PolicyTestJson.Rule(id: $"LEAF-{index:D3}"));
        return string.Join(',', logicalRules.Concat(leafRules));
    }

    private static int CountNodes(PolicyEvaluationResult result)
    {
        return result.Rules.Sum(rule => CountNodes(rule.Condition));
    }

    private static int CountNodes(PolicyConditionEvaluation condition)
    {
        return 1 + condition.Children.Sum(CountNodes);
    }
}
