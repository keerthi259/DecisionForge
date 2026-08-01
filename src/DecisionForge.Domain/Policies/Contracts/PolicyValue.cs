namespace DecisionForge.Domain.Policies.Contracts;

public abstract record PolicyValue
{
    private protected PolicyValue()
    {
    }
}

public sealed record PolicyStringValue : PolicyValue
{
    internal PolicyStringValue(string value)
    {
        Value = value;
    }

    public string Value { get; }
}

public sealed record PolicyNumberValue : PolicyValue
{
    internal PolicyNumberValue(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }
}

public sealed record PolicyBooleanValue : PolicyValue
{
    internal PolicyBooleanValue(bool value)
    {
        Value = value;
    }

    public bool Value { get; }
}
