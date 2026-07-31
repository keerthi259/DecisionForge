using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

public sealed record Money
{
    public const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    public static Money Create(decimal amount, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ValidateAmount(amount, nameof(amount));
        return new Money(amount, currency);
    }

    public static Money Zero(CurrencyCode currency)
    {
        return Create(0m, currency);
    }

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        EnsureSameCurrency(other);

        decimal result = Calculate(() => checked(Amount + other.Amount));
        return result > MaximumAmount ? throw AmountOverflow() : Create(result, Currency);
    }

    public Money Multiply(int multiplier)
    {
        if (multiplier <= 0)
        {
            throw DomainGuard.Validation(nameof(multiplier), "Money multiplier must be positive.");
        }

        decimal result = Calculate(() => checked(Amount * multiplier));
        return result > MaximumAmount ? throw AmountOverflow() : Create(result, Currency);
    }

    private static void ValidateAmount(decimal amount, string parameterName)
    {
        if (amount < 0m || amount > MaximumAmount)
        {
            throw DomainGuard.Validation(
                parameterName,
                $"Money amount must be between 0 and {MaximumAmount}.");
        }

        if (decimal.Round(amount, 2, MidpointRounding.ToEven) != amount)
        {
            throw DomainGuard.Validation(parameterName, "Money amount supports at most two decimals.");
        }
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new DomainRuleException(
                DomainErrorCodes.CurrencyMismatch,
                "Money values must use the same currency.");
        }
    }

    private static decimal Calculate(Func<decimal> calculation)
    {
        try
        {
            return calculation();
        }
        catch (OverflowException)
        {
            throw AmountOverflow();
        }
    }

    private static DomainRuleException AmountOverflow()
    {
        return new DomainRuleException(
            DomainErrorCodes.AmountOverflow,
            "Money arithmetic exceeded the supported maximum.");
    }
}
