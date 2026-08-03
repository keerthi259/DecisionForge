using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Builders;

internal sealed class PurchaseRequestBuilder
{
    public static readonly Guid DefaultRequestId = Guid.Parse("11111111-1111-7111-8111-111111111111");
    public static readonly Guid DefaultRequesterId = Guid.Parse("22222222-2222-7222-8222-222222222222");
    public static readonly Guid DefaultDepartmentId = Guid.Parse("33333333-3333-7333-8333-333333333333");
    public static readonly Guid DefaultSupplierId = Guid.Parse("44444444-4444-7444-8444-444444444444");
    public static readonly DateTimeOffset DefaultTime = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly List<ItemSpecification> _items = [];
    private readonly Guid _id = DefaultRequestId;
    private Guid _requesterId = DefaultRequesterId;
    private readonly CurrencyCode _currency = CurrencyCode.Parse("INR");
    private PurchaseRequestMetadata _metadata = DefaultMetadata();

    public PurchaseRequestBuilder WithItem(
        Guid? id = null,
        int quantity = 2,
        decimal unitPrice = 1_250m,
        ProcurementCategory category = ProcurementCategory.Hardware)
    {
        _items.Add(new ItemSpecification(
            id ?? PurchaseRequestItemBuilder.DefaultItemId,
            quantity,
            unitPrice,
            category));
        return this;
    }

    public PurchaseRequestBuilder WithRequester(Guid requesterId)
    {
        _requesterId = requesterId;
        return this;
    }

    public PurchaseRequestBuilder WithMetadata(PurchaseRequestMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public PurchaseRequest Build()
    {
        PurchaseRequest request = PurchaseRequest.Create(
            _id,
            RequestNumber.Parse("PR-2026-000001"),
            _requesterId,
            _currency,
            _metadata,
            Token(0),
            DefaultTime);

        for (int index = 0; index < _items.Count; index++)
        {
            ItemSpecification item = _items[index];
            request.AddItem(
                item.Id,
                "Developer laptop",
                item.Quantity,
                Money.Create(item.UnitPrice, _currency),
                item.Category,
                request.ConcurrencyToken,
                Token(index + 1),
                DefaultTime);
        }

        return request;
    }

    public static PurchaseRequestMetadata DefaultMetadata()
    {
        return PurchaseRequestMetadata.Create(
            DefaultDepartmentId,
            DefaultSupplierId,
            Urgency.Normal,
            DataSensitivity.Internal,
            new DateOnly(2026, 8, 31),
            BusinessJustification.Parse("Supports the committed customer delivery."));
    }

    public static ConcurrencyToken Token(int sequence)
    {
        return ConcurrencyToken.Create(Guid.Parse($"55555555-5555-7555-8555-{sequence:000000000000}"));
    }

    private sealed record ItemSpecification(
        Guid Id,
        int Quantity,
        decimal UnitPrice,
        ProcurementCategory Category);
}
