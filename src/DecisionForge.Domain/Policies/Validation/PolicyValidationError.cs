using System.Collections.ObjectModel;
using DecisionForge.Domain.Policies.Contracts;

namespace DecisionForge.Domain.Policies.Validation;

public sealed record PolicyValidationError
{
    internal PolicyValidationError(
        string path,
        string code,
        PolicyValidationSeverity severity,
        string message)
    {
        Path = path;
        Code = code;
        Severity = severity;
        Message = message;
    }

    public string Path { get; }

    public string Code { get; }

    public PolicyValidationSeverity Severity { get; }

    public string Message { get; }
}

public sealed record PolicyParseResult
{
    internal PolicyParseResult(
        PolicyDefinition? definition,
        IEnumerable<PolicyValidationError> errors)
    {
        Definition = definition;
        Errors = new ReadOnlyCollection<PolicyValidationError>(errors.ToArray());
    }

    public PolicyDefinition? Definition { get; }

    public IReadOnlyList<PolicyValidationError> Errors { get; }

    public bool IsValid => Definition is not null && Errors.Count == 0;
}
