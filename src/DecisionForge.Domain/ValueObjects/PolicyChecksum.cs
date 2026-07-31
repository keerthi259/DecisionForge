namespace DecisionForge.Domain.ValueObjects;

public sealed record PolicyChecksum
{
    private PolicyChecksum(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static PolicyChecksum Parse(string value)
    {
        return new PolicyChecksum(StringValueValidation.Hash(value, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
