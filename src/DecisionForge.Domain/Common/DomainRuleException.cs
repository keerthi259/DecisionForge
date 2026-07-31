namespace DecisionForge.Domain.Common;

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string code, string message, string? parameterName = null)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        ParameterName = parameterName;
    }

    public string Code { get; }

    public string? ParameterName { get; }
}
