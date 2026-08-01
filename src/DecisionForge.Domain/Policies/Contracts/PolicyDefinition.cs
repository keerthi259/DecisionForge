using System.Collections.ObjectModel;
using DecisionForge.Domain.Policies.Conditions;

namespace DecisionForge.Domain.Policies.Contracts;

public sealed record PolicyRule
{
    internal PolicyRule(
        string id,
        int priority,
        PolicyCondition when,
        PolicyOutcome then)
    {
        Id = id;
        Priority = priority;
        When = when;
        Then = then;
    }

    public string Id { get; }

    public int Priority { get; }

    public PolicyCondition When { get; }

    public PolicyOutcome Then { get; }
}

public sealed record PolicyDefinition
{
    internal PolicyDefinition(
        string schemaVersion,
        string policyCode,
        string name,
        PolicyOutcome defaultOutcome,
        IEnumerable<PolicyRule> rules)
    {
        SchemaVersion = schemaVersion;
        PolicyCode = policyCode;
        Name = name;
        DefaultOutcome = defaultOutcome;
        Rules = new ReadOnlyCollection<PolicyRule>(rules.ToArray());
    }

    public string SchemaVersion { get; }

    public string PolicyCode { get; }

    public string Name { get; }

    public PolicyOutcome DefaultOutcome { get; }

    public IReadOnlyList<PolicyRule> Rules { get; }
}
