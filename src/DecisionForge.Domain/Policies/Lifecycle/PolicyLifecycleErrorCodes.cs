namespace DecisionForge.Domain.Policies.Lifecycle;

public static class PolicyLifecycleErrorCodes
{
    public const string InvalidDefinition = "policy.invalid";
    public const string ImmutableVersion = "policy.published-immutable";
    public const string EffectiveRangeOverlap = "policy.effective-range-overlap";
}
