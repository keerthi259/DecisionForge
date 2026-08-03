namespace DecisionForge.Domain.ValueObjects;

public sealed record PolicyCode
{
    private const int _maximumLength = 64;

    private PolicyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PolicyCode Parse(string? value)
    {
        return new PolicyCode(StringValueValidation.Code(value, _maximumLength, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
