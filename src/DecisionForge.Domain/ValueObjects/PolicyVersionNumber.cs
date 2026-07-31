using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

public sealed record PolicyVersionNumber
{
    private PolicyVersionNumber(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static PolicyVersionNumber Create(int value)
    {
        if (value <= 0)
        {
            throw DomainGuard.Validation(nameof(value), "Policy version number must be positive.");
        }

        return new PolicyVersionNumber(value);
    }

    public PolicyVersionNumber Next()
    {
        if (Value == int.MaxValue)
        {
            throw new DomainRuleException(
                DomainErrorCodes.AmountOverflow,
                "Policy version number cannot be incremented further.");
        }

        return Create(Value + 1);
    }

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
