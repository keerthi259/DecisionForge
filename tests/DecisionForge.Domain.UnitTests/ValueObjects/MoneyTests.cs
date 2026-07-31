using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    private static readonly CurrencyCode _inr = CurrencyCode.Parse("INR");

    [Theory]
    [InlineData("INR", "INR")]
    [InlineData(" inr ", "INR")]
    [InlineData("USD", "USD")]
    public void CurrencyCodeNormalizesValidInput(string input, string expected)
    {
        CurrencyCode currency = CurrencyCode.Parse(input);

        Assert.Equal(expected, currency.Value);
        Assert.Equal(expected, currency.ToString());
        Assert.Equal(currency, CurrencyCode.Parse(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("IN")]
    [InlineData("EURO")]
    [InlineData("I1R")]
    [InlineData("I-R")]
    public void CurrencyCodeRejectsInvalidInput(string? input)
    {
        AssertValidation(() => CurrencyCode.Parse(input!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(1234.56)]
    public void MoneyAcceptsSupportedAmounts(double input)
    {
        decimal amount = Convert.ToDecimal(input, System.Globalization.CultureInfo.InvariantCulture);

        Money money = Money.Create(amount, _inr);

        Assert.Equal(amount, money.Amount);
        Assert.Equal(_inr, money.Currency);
    }

    [Fact]
    public void MoneyAcceptsMaximumAmount()
    {
        Assert.Equal(Money.MaximumAmount, Money.Create(Money.MaximumAmount, _inr).Amount);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.001)]
    public void MoneyRejectsInvalidAmounts(double input)
    {
        decimal amount = Convert.ToDecimal(input, System.Globalization.CultureInfo.InvariantCulture);

        AssertValidation(() => Money.Create(amount, _inr));
    }

    [Fact]
    public void MoneyRejectsAmountAboveStorageBoundaryAndNullCurrency()
    {
        AssertValidation(() => Money.Create(Money.MaximumAmount + 0.01m, _inr));
        Assert.Throws<ArgumentNullException>(() => Money.Create(1m, null!));
    }

    [Fact]
    public void AdditionAndMultiplicationPreserveCurrencyAndPrecision()
    {
        Money first = Money.Create(10.25m, _inr);
        Money second = Money.Create(20.10m, _inr);

        Assert.Equal(Money.Create(30.35m, _inr), first.Add(second));
        Assert.Equal(Money.Create(30.75m, _inr), first.Multiply(3));
        Assert.Equal(Money.Zero(_inr), Money.Create(0m, _inr));
    }

    [Fact]
    public void ArithmeticRejectsInvalidMultiplierAndCurrencyMismatch()
    {
        DomainRuleException mismatch = Assert.Throws<DomainRuleException>(
            () => Money.Create(1m, _inr).Add(Money.Create(1m, CurrencyCode.Parse("USD"))));

        Assert.Equal(DomainErrorCodes.CurrencyMismatch, mismatch.Code);
        AssertValidation(() => Money.Create(1m, _inr).Multiply(0));
        Assert.Throws<ArgumentNullException>(() => Money.Create(1m, _inr).Add(null!));
    }

    [Fact]
    public void ArithmeticOverflowUsesStableErrorAndDoesNotWrap()
    {
        DomainRuleException addition = Assert.Throws<DomainRuleException>(
            () => Money.Create(Money.MaximumAmount, _inr).Add(Money.Create(0.01m, _inr)));
        DomainRuleException multiplication = Assert.Throws<DomainRuleException>(
            () => Money.Create(Money.MaximumAmount, _inr).Multiply(int.MaxValue));

        Assert.Equal(DomainErrorCodes.AmountOverflow, addition.Code);
        Assert.Equal(DomainErrorCodes.AmountOverflow, multiplication.Code);
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
