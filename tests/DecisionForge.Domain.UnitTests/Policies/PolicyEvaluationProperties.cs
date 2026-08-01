using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;
using FsCheck;
using FsCheck.Xunit;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyEvaluationProperties
{
    [Property(MaxTest = 100)]
    public bool RejectionAlwaysPrecedesManualAndDefault(bool rejectionMatches, bool manualMatches)
    {
        string rejection = Rule(
            "REJECTION",
            20,
            rejectionMatches,
            "Rejected",
            null,
            "REJECTION",
            "Rejected.");
        string manual = Rule(
            "MANUAL",
            10,
            manualMatches,
            "ManualApprovalRequired",
            "[\"FinanceApprover\"]",
            "MANUAL",
            "Manual review.");
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(string.Join(',', rejection, manual))),
            PolicyFactSet.Create([PolicyFact.Logical("supplier.isActive", true)]));
        DecisionDisposition expected = rejectionMatches
            ? DecisionDisposition.Rejected
            : manualMatches
                ? DecisionDisposition.ManualApprovalRequired
                : DecisionDisposition.AutoApproved;

        return result.Disposition == expected;
    }

    [Property(MaxTest = 100)]
    public bool DuplicateRolesAndReasonsAlwaysCollapse(PositiveInt input)
    {
        int count = input.Get % 20 + 1;
        string rules = string.Join(
            ',',
            Enumerable.Range(1, count).Select(index => Rule(
                $"RULE-{index}",
                index,
                matches: true,
                "ManualApprovalRequired",
                "[\"FinanceApprover\"]",
                "FINANCE",
                "Finance review.")));
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(rules)),
            PolicyFactSet.Create([PolicyFact.Logical("supplier.isActive", true)]));

        return result.RequiredApproverRoles.SequenceEqual(
                [PolicyApproverRole.FinanceApprover])
            && result.Reasons.Count == 1
            && result.Reasons[0].Code.Value == "FINANCE";
    }

    [Property(MaxTest = 100)]
    public bool RuleInputPermutationDoesNotChangeTrace(PositiveInt input)
    {
        int count = input.Get % 20 + 1;
        string[] rules = Enumerable.Range(1, count)
            .Select(index => Rule(
                $"RULE-{index:D2}",
                count - index,
                matches: index % 2 == 0,
                "Rejected",
                null,
                $"REASON-{index:D2}",
                $"Reason {index}."))
            .ToArray();
        PolicyFactSet facts = PolicyFactSet.Create(
            [PolicyFact.Logical("supplier.isActive", true)]);
        PolicyEvaluationResult forward = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(string.Join(',', rules))),
            facts);
        PolicyEvaluationResult reverse = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(PolicyTestJson.Policy(
                string.Join(',', rules.Reverse()))),
            facts);

        return forward.TraceChecksum == reverse.TraceChecksum;
    }

    private static string Rule(
        string id,
        int priority,
        bool matches,
        string disposition,
        string? roles,
        string reasonCode,
        string message)
    {
        string condition = $$"""
        {"fact":"supplier.isActive","operator":"equals","value":{{matches.ToString().ToLowerInvariant()}}}
        """;
        return PolicyTestJson.Rule(
            condition,
            PolicyTestJson.Outcome(disposition, roles, reasonCode, message),
            id,
            priority);
    }
}
