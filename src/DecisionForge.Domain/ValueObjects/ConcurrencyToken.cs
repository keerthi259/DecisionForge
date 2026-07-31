using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

public sealed record ConcurrencyToken
{
    private ConcurrencyToken(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static ConcurrencyToken Create(Guid value)
    {
        return new ConcurrencyToken(DomainGuard.NotEmpty(value, nameof(value)));
    }

    public override string ToString()
    {
        return Value.ToString("N");
    }
}
