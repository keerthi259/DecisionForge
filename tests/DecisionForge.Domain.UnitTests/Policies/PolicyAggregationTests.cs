using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyAggregationTests
{
    [Fact]
    public void RulesAreEvaluatedByPriorityThenOrdinalIdRegardlessOfJsonOrder()
    {
        string first = PolicyTestJson.Rule(id: "Z-RULE", priority: 10);
        string second = PolicyTestJson.Rule(id: "A-RULE", priority: 10);
        string third = PolicyTestJson.Rule(id: "FIRST", priority: 1);
        PolicyFactSet facts = PolicyFactSet.Create(
            [PolicyFact.Logical("supplier.isActive", true)]);

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(string.Join(',', first, third, second))),
            facts);
        PolicyEvaluationResult reordered = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(string.Join(',', second, first, third))),
            facts);

        Assert.Equal(["FIRST", "A-RULE", "Z-RULE"], result.Rules.Select(rule => rule.RuleId));
        Assert.Equal(result.TraceChecksum, reordered.TraceChecksum);
    }

    [Fact]
    public void RejectionPrecedesManualWhichPrecedesDefault()
    {
        PolicyFactSet facts = PolicyFactSet.Create(
            [PolicyFact.Logical("supplier.isActive", true)]);
        string manual = PolicyTestJson.Rule(
            outcome: PolicyTestJson.Outcome(
                "ManualApprovalRequired",
                "[\"FinanceApprover\"]",
                "MANUAL",
                "Manual review."),
            id: "MANUAL",
            priority: 1);
        string rejected = PolicyTestJson.Rule(
            outcome: PolicyTestJson.Outcome(
                "Rejected",
                null,
                "REJECTED",
                "Rejected."),
            id: "REJECTED",
            priority: 2);

        PolicyEvaluationResult both = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(string.Join(',', manual, rejected))),
            facts);
        PolicyEvaluationResult onlyManual = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(manual)),
            facts);
        PolicyEvaluationResult noMatch = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.SingleRule(
                "supplier.isActive",
                "equals",
                "false"),
            facts);

        Assert.Equal(DecisionDisposition.Rejected, both.Disposition);
        Assert.Equal(DecisionDisposition.ManualApprovalRequired, onlyManual.Disposition);
        Assert.Equal(DecisionDisposition.AutoApproved, noMatch.Disposition);
        Assert.False(both.DefaultOutcomeApplied);
        Assert.False(onlyManual.DefaultOutcomeApplied);
        Assert.True(noMatch.DefaultOutcomeApplied);
    }

    [Fact]
    public void RolesAndReasonsAreDeduplicatedInControlledOrder()
    {
        string finance = ManualRule(
            "FINANCE-1",
            30,
            "[\"FinanceApprover\",\"SeniorApprover\"]",
            "FINANCE",
            "Finance review.");
        string security = ManualRule(
            "SECURITY",
            10,
            "[\"SecurityApprover\",\"FinanceApprover\"]",
            "SECURITY",
            "Security review.");
        string financeDuplicate = ManualRule(
            "FINANCE-2",
            20,
            "[\"FinanceApprover\"]",
            "FINANCE",
            "Finance review.");

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(
                string.Join(',', finance, security, financeDuplicate))),
            PolicyFactSet.Create([PolicyFact.Logical("supplier.isActive", true)]));

        Assert.Equal(
            [
                PolicyApproverRole.SecurityApprover,
                PolicyApproverRole.FinanceApprover,
                PolicyApproverRole.SeniorApprover,
            ],
            result.RequiredApproverRoles);
        Assert.Equal(["SECURITY", "FINANCE"], result.Reasons.Select(reason => reason.Code.Value));
    }

    private static string ManualRule(
        string id,
        int priority,
        string roles,
        string reasonCode,
        string message)
    {
        return PolicyTestJson.Rule(
            outcome: PolicyTestJson.Outcome(
                "ManualApprovalRequired",
                roles,
                reasonCode,
                message),
            id: id,
            priority: priority);
    }
}
