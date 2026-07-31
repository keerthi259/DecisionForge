using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Builders;

internal sealed class PurchaseRequestItemBuilder
{
    public static readonly Guid DefaultItemId = Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaaa");

    private Guid _id = DefaultItemId;
    private readonly string _description = "Developer laptop";
    private int _quantity = 2;
    private Money _unitPrice = Money.Create(1_250m, CurrencyCode.Parse("INR"));
    private readonly ProcurementCategory _category = ProcurementCategory.Hardware;

    public PurchaseRequestItemBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public PurchaseRequestItemBuilder WithQuantity(int quantity)
    {
        _quantity = quantity;
        return this;
    }

    public PurchaseRequestItemBuilder WithUnitPrice(Money unitPrice)
    {
        _unitPrice = unitPrice;
        return this;
    }

    public PurchaseRequestItem Build()
    {
        return PurchaseRequestItem.Create(
            _id,
            _description,
            _quantity,
            _unitPrice,
            _category);
    }
}
