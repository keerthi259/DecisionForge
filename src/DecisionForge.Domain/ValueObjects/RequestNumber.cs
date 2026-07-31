namespace DecisionForge.Domain.ValueObjects;

public sealed record RequestNumber
{
    private RequestNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static RequestNumber Parse(string value)
    {
        return new RequestNumber(StringValueValidation.Code(value, 32, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
