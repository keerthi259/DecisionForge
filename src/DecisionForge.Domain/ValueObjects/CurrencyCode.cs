using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

public sealed record CurrencyCode
{
    private CurrencyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CurrencyCode Parse(string value)
    {
        string normalized = StringValueValidation.Code(value, 3, nameof(value), string.Empty);
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw DomainGuard.Validation(nameof(value), "Currency code must contain three letters.");
        }

        return new CurrencyCode(normalized);
    }

    public override string ToString()
    {
        return Value;
    }
}
