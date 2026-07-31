using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestItemTests
{
    private static readonly CurrencyCode _inr = CurrencyCode.Parse("INR");

    [Fact]
    public void CreateCalculatesLineTotalAtMinimumBoundary()
    {
        PurchaseRequestItem item = new PurchaseRequestItemBuilder()
            .WithQuantity(1)
            .WithUnitPrice(Money.Create(0.01m, _inr))
            .Build();

        Assert.Equal(Money.Create(0.01m, _inr), item.LineTotal);
        Assert.Equal(ProcurementCategory.Hardware, item.Category);
        Assert.Equal("Developer laptop", item.Description);
    }

    [Fact]
    public void CreateAcceptsMaximumLineTotalBoundary()
    {
        PurchaseRequestItem item = new PurchaseRequestItemBuilder()
            .WithQuantity(1)
            .WithUnitPrice(Money.Create(Money.MaximumAmount, _inr))
            .Build();

        Assert.Equal(Money.MaximumAmount, item.LineTotal.Amount);
    }

    [Fact]
    public void CreateNormalizesDescription()
    {
        PurchaseRequestItem item = PurchaseRequestItem.Create(
            PurchaseRequestItemBuilder.DefaultItemId,
            "  Laptop  ",
            1,
            Money.Create(1m, _inr),
            ProcurementCategory.Hardware);

        Assert.Equal("Laptop", item.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateRejectsNonPositiveQuantity(int quantity)
    {
        AssertValidation(
            () => new PurchaseRequestItemBuilder().WithQuantity(quantity).Build());
    }

    [Fact]
    public void CreateRejectsEmptyIdentifierDescriptionAndUnitPrice()
    {
        AssertValidation(() => new PurchaseRequestItemBuilder().WithId(Guid.Empty).Build());
        AssertValidation(
            () => PurchaseRequestItem.Create(
                PurchaseRequestItemBuilder.DefaultItemId,
                " ",
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Hardware));
        AssertValidation(
            () => PurchaseRequestItem.Create(
                PurchaseRequestItemBuilder.DefaultItemId,
                new string('x', PurchaseRequestItem.DescriptionMaximumLength + 1),
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Hardware));
        AssertValidation(
            () => PurchaseRequestItem.Create(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Laptop",
                1,
                Money.Zero(_inr),
                ProcurementCategory.Hardware));
    }

    [Fact]
    public void CreateRejectsInvalidCategoryAndLineOverflow()
    {
        AssertValidation(
            () => PurchaseRequestItem.Create(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Laptop",
                1,
                Money.Create(1m, _inr),
                (ProcurementCategory)999));

        DomainRuleException overflow = Assert.Throws<DomainRuleException>(
            () => new PurchaseRequestItemBuilder()
                .WithQuantity(2)
                .WithUnitPrice(Money.Create(Money.MaximumAmount, _inr))
                .Build());
        Assert.Equal(DomainErrorCodes.AmountOverflow, overflow.Code);
    }

    [Fact]
    public void CreateRejectsNullUnitPrice()
    {
        Assert.Throws<ArgumentNullException>(
            () => PurchaseRequestItem.Create(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Laptop",
                1,
                null!,
                ProcurementCategory.Hardware));
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
