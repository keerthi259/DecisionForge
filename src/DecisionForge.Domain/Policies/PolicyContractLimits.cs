namespace DecisionForge.Domain.Policies;

public static class PolicyContractLimits
{
    public const string SupportedSchemaVersion = "1.0";
    public const int MaximumJsonBytes = 256 * 1024;
    public const int MaximumRules = 100;
    public const int MaximumConditionDepth = 10;
    public const int MaximumConditionChildren = 25;
    public const int MaximumMembershipValues = 100;
    public const int MaximumRuleIdLength = 64;
    public const int MaximumReasonCodeLength = 64;
    public const int MaximumReasonMessageLength = 500;
    public const int MaximumPolicyCodeLength = 64;
    public const int MaximumPolicyNameLength = 200;
}
