namespace DecisionForge.Domain.ValueObjects;

public sealed record ReasonCode
{
    private ReasonCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ReasonCode Parse(string value)
    {
        return new ReasonCode(StringValueValidation.Code(value, 64, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
