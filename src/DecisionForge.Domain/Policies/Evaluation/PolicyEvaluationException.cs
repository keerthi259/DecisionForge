namespace DecisionForge.Domain.Policies.Evaluation;

public static class PolicyEvaluationErrorCodes
{
    public const string UnknownFact = "policy.evaluation.unknown-fact";
    public const string MissingFact = "policy.evaluation.missing-fact";
    public const string FactTypeMismatch = "policy.evaluation.fact-type";
    public const string DuplicateFact = "policy.evaluation.duplicate-fact";
    public const string InvalidPolicy = "policy.evaluation.invalid-policy";
    public const string ExecutionLimit = "policy.evaluation.execution-limit";
}

public sealed class PolicyEvaluationException : Exception
{
    internal PolicyEvaluationException(string code, string path, string message)
        : base(message)
    {
        Code = code;
        Path = path;
    }

    public string Code { get; }

    public string Path { get; }
}
