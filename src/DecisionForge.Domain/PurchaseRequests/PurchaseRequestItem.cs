using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed class PurchaseRequestItem : Entity
{
    public const int DescriptionMaximumLength = 200;

    private PurchaseRequestItem(
        Guid id,
        string description,
        int quantity,
        Money unitPrice,
        ProcurementCategory category)
        : base(id)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Category = category;
    }

    public string Description { get; private set; }

    public int Quantity { get; private set; }

    public Money UnitPrice { get; private set; }

    public ProcurementCategory Category { get; private set; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    public static PurchaseRequestItem Create(
        Guid id,
        string description,
        int quantity,
        Money unitPrice,
        ProcurementCategory category)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);
        Validate(description, quantity, unitPrice, category);
        return new PurchaseRequestItem(id, description.Trim(), quantity, unitPrice, category);
    }

    internal bool Change(
        string description,
        int quantity,
        Money unitPrice,
        ProcurementCategory category)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);
        Validate(description, quantity, unitPrice, category);
        string normalizedDescription = description.Trim();

        if (Description == normalizedDescription
            && Quantity == quantity
            && UnitPrice == unitPrice
            && Category == category)
        {
            return false;
        }

        Description = normalizedDescription;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Category = category;
        return true;
    }

    private static void Validate(
        string? description,
        int quantity,
        Money unitPrice,
        ProcurementCategory category)
    {
        if (string.IsNullOrWhiteSpace(description)
            || description.Trim().Length > DescriptionMaximumLength)
        {
            throw DomainGuard.Validation(
                nameof(description),
                $"Item description must contain between 1 and {DescriptionMaximumLength} characters.");
        }

        if (quantity <= 0)
        {
            throw DomainGuard.Validation(nameof(quantity), "Item quantity must be positive.");
        }

        if (unitPrice.Amount <= 0m)
        {
            throw DomainGuard.Validation(nameof(unitPrice), "Item unit price must be positive.");
        }

        if (!Enum.IsDefined(category))
        {
            throw DomainGuard.Validation(nameof(category), "Procurement category is not supported.");
        }

        _ = unitPrice.Multiply(quantity);
    }
}
