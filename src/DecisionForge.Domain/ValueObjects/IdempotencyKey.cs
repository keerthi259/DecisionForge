using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

public sealed record IdempotencyKey
{
    public const int MaximumLength = 128;

    private IdempotencyKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static IdempotencyKey Parse(string value)
    {
        string normalized = StringValueValidation.Required(value, MaximumLength, nameof(value));
        if (normalized.Any(character => character is < '!' or > '~'))
        {
            throw DomainGuard.Validation(
                nameof(value),
                "Idempotency key must contain visible ASCII characters without spaces.");
        }

        return new IdempotencyKey(normalized);
    }

    public override string ToString()
    {
        return Value;
    }
}
