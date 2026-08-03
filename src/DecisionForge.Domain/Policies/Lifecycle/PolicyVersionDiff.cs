using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Lifecycle;

public sealed record PolicyRuleModification
{
    internal PolicyRuleModification(
        string ruleId,
        bool priorityChanged,
        bool conditionChanged,
        bool outcomeChanged)
    {
        RuleId = ruleId;
        PriorityChanged = priorityChanged;
        ConditionChanged = conditionChanged;
        OutcomeChanged = outcomeChanged;
    }

    public string RuleId { get; }

    public bool PriorityChanged { get; }

    public bool ConditionChanged { get; }

    public bool OutcomeChanged { get; }
}

public sealed record PolicyVersionDiff
{
    internal PolicyVersionDiff(
        PolicyVersionNumber fromVersion,
        PolicyVersionNumber toVersion,
        bool defaultOutcomeChanged,
        IEnumerable<string> addedRuleIds,
        IEnumerable<string> removedRuleIds,
        IEnumerable<PolicyRuleModification> modifiedRules)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
        DefaultOutcomeChanged = defaultOutcomeChanged;
        AddedRuleIds = new ReadOnlyCollection<string>(addedRuleIds.ToArray());
        RemovedRuleIds = new ReadOnlyCollection<string>(removedRuleIds.ToArray());
        ModifiedRules = new ReadOnlyCollection<PolicyRuleModification>(
            modifiedRules.ToArray());
    }

    public PolicyVersionNumber FromVersion { get; }

    public PolicyVersionNumber ToVersion { get; }

    public bool DefaultOutcomeChanged { get; }

    public IReadOnlyList<string> AddedRuleIds { get; }

    public IReadOnlyList<string> RemovedRuleIds { get; }

    public IReadOnlyList<PolicyRuleModification> ModifiedRules { get; }

    public bool HasChanges => DefaultOutcomeChanged
        || AddedRuleIds.Count > 0
        || RemovedRuleIds.Count > 0
        || ModifiedRules.Count > 0;
}

public static class PolicyVersionComparer
{
    public static PolicyVersionDiff Compare(PolicyVersion from, PolicyVersion to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        PolicyDefinition fromDefinition = RequireDefinition(from);
        PolicyDefinition toDefinition = RequireDefinition(to);
        Dictionary<string, PolicyRule> fromRules = fromDefinition.Rules.ToDictionary(
            rule => rule.Id,
            StringComparer.Ordinal);
        Dictionary<string, PolicyRule> toRules = toDefinition.Rules.ToDictionary(
            rule => rule.Id,
            StringComparer.Ordinal);
        string[] added = toRules.Keys
            .Except(fromRules.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] removed = fromRules.Keys
            .Except(toRules.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        PolicyRuleModification[] modified = fromRules.Keys
            .Intersect(toRules.Keys, StringComparer.Ordinal)
            .Select(ruleId => Modification(fromRules[ruleId], toRules[ruleId]))
            .Where(change => change is not null)
            .Select(change => change!)
            .OrderBy(change => change.RuleId, StringComparer.Ordinal)
            .ToArray();
        bool defaultChanged = !string.Equals(
            PolicyCanonicalSerializer.SerializeOutcome(fromDefinition.DefaultOutcome),
            PolicyCanonicalSerializer.SerializeOutcome(toDefinition.DefaultOutcome),
            StringComparison.Ordinal);
        return new PolicyVersionDiff(
            from.Number,
            to.Number,
            defaultChanged,
            added,
            removed,
            modified);
    }

    private static PolicyRuleModification? Modification(
        PolicyRule from,
        PolicyRule to)
    {
        bool priorityChanged = from.Priority != to.Priority;
        bool conditionChanged = !string.Equals(
            PolicyCanonicalSerializer.SerializeCondition(from.When),
            PolicyCanonicalSerializer.SerializeCondition(to.When),
            StringComparison.Ordinal);
        bool outcomeChanged = !string.Equals(
            PolicyCanonicalSerializer.SerializeOutcome(from.Then),
            PolicyCanonicalSerializer.SerializeOutcome(to.Then),
            StringComparison.Ordinal);
        return priorityChanged || conditionChanged || outcomeChanged
            ? new PolicyRuleModification(
                from.Id,
                priorityChanged,
                conditionChanged,
                outcomeChanged)
            : null;
    }

    private static PolicyDefinition RequireDefinition(PolicyVersion version)
    {
        return version.Definition
            ?? throw new DomainRuleException(
                PolicyLifecycleErrorCodes.InvalidDefinition,
                "Only valid policy versions can be compared.");
    }
}
