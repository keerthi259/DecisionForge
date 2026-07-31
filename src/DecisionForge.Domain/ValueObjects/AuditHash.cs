namespace DecisionForge.Domain.ValueObjects;

public sealed record AuditHash
{
    private AuditHash(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static AuditHash Parse(string value)
    {
        return new AuditHash(StringValueValidation.Hash(value, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
