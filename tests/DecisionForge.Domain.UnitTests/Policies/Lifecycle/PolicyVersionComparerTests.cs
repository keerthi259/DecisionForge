using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies.Lifecycle;

namespace DecisionForge.Domain.UnitTests.Policies.Lifecycle;

public sealed class PolicyVersionComparerTests
{
    [Fact]
    public void CompareIdentifiesAddedRemovedAndStructuredModifiedRules()
    {
        string firstRules = string.Join(
            ',',
            PolicyLifecycleTestData.Rule("RULE-A", 10),
            PolicyLifecycleTestData.Rule("RULE-B", 20, reasonCode: "RULE_B"));
        Policy policy = PolicyLifecycleTestData.Create(
            PolicyLifecycleTestData.Definition(firstRules));
        PolicyVersion first = Assert.Single(policy.Versions);
        policy.Publish(
            first.Id,
            PolicyLifecycleTestData.CreatedAt.AddHours(1),
            PolicyLifecycleTestData.CreatedAt.AddHours(2),
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        string secondRules = string.Join(
            ',',
            PolicyLifecycleTestData.Rule(
                "RULE-A",
                11,
                fact: "supplier.isApproved",
                disposition: "AutoApproved",
                reasonCode: "RULE_A_CHANGED",
                message: "Rule A changed."),
            PolicyLifecycleTestData.Rule("RULE-C", 30, reasonCode: "RULE_C"));
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(
                secondRules,
                PolicyTestJson.Outcome(
                    reasonCode: "NEW_DEFAULT",
                    message: "The new default applies.")),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        PolicyVersionDiff diff = PolicyVersionComparer.Compare(first, second);

        Assert.True(diff.HasChanges);
        Assert.Equal(1, diff.FromVersion.Value);
        Assert.Equal(2, diff.ToVersion.Value);
        Assert.True(diff.DefaultOutcomeChanged);
        Assert.Equal(["RULE-C"], diff.AddedRuleIds);
        Assert.Equal(["RULE-B"], diff.RemovedRuleIds);
        PolicyRuleModification modified = Assert.Single(diff.ModifiedRules);
        Assert.Equal("RULE-A", modified.RuleId);
        Assert.True(modified.PriorityChanged);
        Assert.True(modified.ConditionChanged);
        Assert.True(modified.OutcomeChanged);
    }

    [Fact]
    public void EquivalentVersionsHaveNoChangesAndImmutableCollections()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        PolicyVersion first = Assert.Single(policy.Versions);
        policy.Publish(
            first.Id,
            PolicyLifecycleTestData.CreatedAt.AddHours(1),
            PolicyLifecycleTestData.CreatedAt.AddHours(2),
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        PolicyVersionDiff diff = PolicyVersionComparer.Compare(first, second);

        Assert.False(diff.HasChanges);
        Assert.False(diff.DefaultOutcomeChanged);
        Assert.Empty(diff.AddedRuleIds);
        Assert.Empty(diff.RemovedRuleIds);
        Assert.Empty(diff.ModifiedRules);
        ICollection<string> added = Assert.IsAssignableFrom<ICollection<string>>(
            diff.AddedRuleIds);
        Assert.Throws<NotSupportedException>(() => added.Add("FORGED"));
    }

    [Fact]
    public void ComparisonRejectsInvalidVersion()
    {
        Policy validPolicy = PolicyLifecycleTestData.Create();
        Policy invalidPolicy = PolicyLifecycleTestData.Create("{ invalid");

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() =>
            PolicyVersionComparer.Compare(
                Assert.Single(validPolicy.Versions),
                Assert.Single(invalidPolicy.Versions)));

        Assert.Equal(PolicyLifecycleErrorCodes.InvalidDefinition, exception.Code);
    }

    [Fact]
    public void ComparisonOrdersRuleIdentifiersOrdinally()
    {
        string firstRules = PolicyLifecycleTestData.Rule("RULE-Z", 1);
        Policy policy = PolicyLifecycleTestData.Create(
            PolicyLifecycleTestData.Definition(firstRules));
        PolicyVersion first = Assert.Single(policy.Versions);
        policy.Publish(
            first.Id,
            PolicyLifecycleTestData.CreatedAt.AddHours(1),
            PolicyLifecycleTestData.CreatedAt.AddHours(2),
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        string secondRules = string.Join(
            ',',
            PolicyLifecycleTestData.Rule("RULE-B", 2, reasonCode: "RULE_B"),
            PolicyLifecycleTestData.Rule("RULE-A", 3, reasonCode: "RULE_C"));
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(secondRules),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        PolicyVersionDiff diff = PolicyVersionComparer.Compare(first, second);

        Assert.Equal(["RULE-A", "RULE-B"], diff.AddedRuleIds);
        Assert.Equal(["RULE-Z"], diff.RemovedRuleIds);
    }
}
