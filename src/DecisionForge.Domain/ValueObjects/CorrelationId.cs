using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

public sealed record CorrelationId
{
    public const int MaximumLength = 128;

    private CorrelationId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CorrelationId Parse(string value)
    {
        string normalized = StringValueValidation.Required(value, MaximumLength, nameof(value));
        if (normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw DomainGuard.Validation(nameof(value), "Correlation ID contains unsupported characters.");
        }

        return new CorrelationId(normalized);
    }

    public override string ToString()
    {
        return Value;
    }
}
