namespace DecisionForge.Domain.Decisions;

public static class DecisionErrorCodes
{
    public const string NoEffectivePolicy = "decision.no-effective-policy";
    public const string AmbiguousEffectivePolicy = "decision.ambiguous-effective-policy";
    public const string PolicyEvidenceMismatch = "decision.policy-evidence-mismatch";
    public const string EvaluationContextMissing = "decision.evaluation-context-missing";
}
