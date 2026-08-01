using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyEvaluationBranchTests
{
    [Fact]
    public void EmptyRuleSetAppliesManualDefaultOutcomeIncludingRoles()
    {
        string defaultOutcome = PolicyTestJson.Outcome(
            "ManualApprovalRequired",
            "[\"DepartmentApprover\"]",
            "DEFAULT_MANUAL",
            "Department review is the default.");
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(rules: string.Empty, defaultOutcome: defaultOutcome)),
            PolicyFactSet.Create([]));

        Assert.Empty(result.Rules);
        Assert.Equal(DecisionDisposition.ManualApprovalRequired, result.Disposition);
        Assert.True(result.DefaultOutcomeApplied);
        Assert.Equal([PolicyApproverRole.DepartmentApprover], result.RequiredApproverRoles);
        Assert.Equal("DEFAULT_MANUAL", Assert.Single(result.Reasons).Code.Value);
    }

    [Fact]
    public void AllAndAnyTruthTablesIncludeFalseResults()
    {
        string all =
            """
            {
              "all": [
                {"fact":"supplier.isActive","operator":"equals","value":true},
                {"fact":"supplier.isApproved","operator":"equals","value":true}
              ]
            }
            """;
        string any =
            """
            {
              "any": [
                {"fact":"supplier.isActive","operator":"equals","value":false},
                {"fact":"supplier.isApproved","operator":"equals","value":true}
              ]
            }
            """;
        string rules = string.Join(
            ',',
            PolicyTestJson.Rule(all, id: "ALL", priority: 1),
            PolicyTestJson.Rule(any, id: "ANY", priority: 2));
        PolicyFactSet facts = PolicyFactSet.Create(
        [
            PolicyFact.Logical("supplier.isActive", true),
            PolicyFact.Logical("supplier.isApproved", false),
        ]);

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(rules)),
            facts);

        Assert.False(result.Rules[0].Condition.Result);
        Assert.False(result.Rules[1].Condition.Result);
        Assert.All(result.Rules.SelectMany(rule => rule.Condition.Children), child =>
            Assert.Single(child.FactAccesses));
    }

    [Fact]
    public void FactSetCollectionsAndTraceCollectionsRejectMutation()
    {
        PolicyFactSet facts = PolicyEvaluationTestData.Facts();
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.GoldenPolicy(),
            facts);
        ICollection<PolicyFact> factCollection =
            Assert.IsAssignableFrom<ICollection<PolicyFact>>(facts.Facts);
        ICollection<PolicyConditionEvaluation> children =
            Assert.IsAssignableFrom<ICollection<PolicyConditionEvaluation>>(
                result.Rules[0].Condition.Children);
        ICollection<PolicyFactAccess> accesses =
            Assert.IsAssignableFrom<ICollection<PolicyFactAccess>>(
                result.Rules[0].Condition.FactAccesses);

        Assert.Throws<NotSupportedException>(factCollection.Clear);
        Assert.Throws<NotSupportedException>(children.Clear);
        Assert.Throws<NotSupportedException>(accesses.Clear);
    }

    [Fact]
    public void FactSetRejectsNullElements()
    {
        Assert.Throws<ArgumentNullException>(() => PolicyFactSet.Create([null!]));
    }

    [Fact]
    public void MatchingAutoApprovalReasonPrecedesAppliedDefaultReason()
    {
        string autoRule = PolicyTestJson.Rule(
            outcome: PolicyTestJson.Outcome(
                "AutoApproved",
                null,
                "MATCHED_AUTO",
                "An automatic rule matched."));
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(autoRule)),
            PolicyFactSet.Create([PolicyFact.Logical("supplier.isActive", true)]));

        Assert.True(result.DefaultOutcomeApplied);
        Assert.Equal(
            ["MATCHED_AUTO", "DEFAULT_OUTCOME"],
            result.Reasons.Select(reason => reason.Code.Value));
    }
}
