namespace DecisionForge.Domain.ValueObjects;

public sealed record BusinessJustification
{
    public const int MaximumLength = 2_000;

    private BusinessJustification(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static BusinessJustification Parse(string value)
    {
        return new BusinessJustification(
            StringValueValidation.Required(value, MaximumLength, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
