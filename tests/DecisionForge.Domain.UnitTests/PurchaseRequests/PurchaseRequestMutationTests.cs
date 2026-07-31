using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestMutationTests
{
    private static readonly CurrencyCode _inr = CurrencyCode.Parse("INR");
    private static readonly DateTimeOffset _later = PurchaseRequestBuilder.DefaultTime.AddMinutes(1);

    [Fact]
    public void ItemMutationsKeepServerCalculatedTotalAndRaiseExactEvents()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();
        request.ClearDomainEvents();

        PurchaseRequestItem item = request.AddItem(
            PurchaseRequestItemBuilder.DefaultItemId,
            "Laptop",
            2,
            Money.Create(100m, _inr),
            ProcurementCategory.Hardware,
            _later);

        Assert.Equal(200m, request.Total.Amount);
        Assert.Same(item, Assert.Single(request.Items));
        PurchaseRequestItemAddedDomainEvent added =
            Assert.IsType<PurchaseRequestItemAddedDomainEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(item.Id, added.ItemId);
        Assert.Equal(2, added.Quantity);
        Assert.Equal(Money.Create(100m, _inr), added.UnitPrice);

        request.UpdateItem(
            item.Id,
            "Laptop and dock",
            3,
            Money.Create(125.50m, _inr),
            ProcurementCategory.Hardware,
            _later.AddMinutes(1));

        Assert.Equal(376.50m, request.Total.Amount);
        Assert.Equal("Laptop and dock", item.Description);
        PurchaseRequestItemChangedDomainEvent changed =
            Assert.IsType<PurchaseRequestItemChangedDomainEvent>(request.DomainEvents[1]);
        Assert.Equal(3, changed.Quantity);

        request.RemoveItem(item.Id, _later.AddMinutes(2));

        Assert.Empty(request.Items);
        Assert.Equal(Money.Zero(_inr), request.Total);
        Assert.IsType<PurchaseRequestItemRemovedDomainEvent>(request.DomainEvents[2]);
        Assert.Equal(_later.AddMinutes(2), request.LastModifiedAt);
    }

    [Fact]
    public void MultipleItemsAreSummedWithoutClientTotalInput()
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithItem(quantity: 2, unitPrice: 10.25m)
            .WithItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaab"),
                quantity: 3,
                unitPrice: 4.50m,
                category: ProcurementCategory.OfficeSupplies)
            .Build();

        Assert.Equal(34m, request.Total.Amount);
        Assert.Equal(2, request.Items.Count);
    }

    [Fact]
    public void MetadataChangeRaisesSafeEventAndIdenticalChangeIsNoOp()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();
        PurchaseRequestMetadata updated = PurchaseRequestMetadata.Create(
            Guid.Parse("33333333-3333-7333-8333-333333333334"),
            Guid.Parse("44444444-4444-7444-8444-444444444445"),
            Urgency.Urgent,
            DataSensitivity.Confidential,
            new DateOnly(2026, 9, 1),
            BusinessJustification.Parse("Urgent delivery commitment."));
        request.ClearDomainEvents();

        request.UpdateMetadata(updated, _later);
        request.UpdateMetadata(updated, _later.AddMinutes(1));

        PurchaseRequestMetadataChangedDomainEvent changed =
            Assert.IsType<PurchaseRequestMetadataChangedDomainEvent>(
                Assert.Single(request.DomainEvents));
        Assert.Equal(updated.DepartmentId, changed.DepartmentId);
        Assert.Equal(updated.SupplierId, changed.SupplierId);
        Assert.Equal(_later, request.LastModifiedAt);
        Assert.DoesNotContain("Urgent delivery commitment", changed.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IdenticalItemUpdateIsNoOp()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.ClearDomainEvents();

        request.UpdateItem(
            PurchaseRequestItemBuilder.DefaultItemId,
            "Developer laptop",
            2,
            Money.Create(1_250m, _inr),
            ProcurementCategory.Hardware,
            _later);

        Assert.Empty(request.DomainEvents);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, request.LastModifiedAt);
    }

    [Fact]
    public void DuplicateAndMissingItemsReturnStableErrors()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();

        DomainRuleException duplicate = Assert.Throws<DomainRuleException>(
            () => request.AddItem(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Duplicate",
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Hardware,
                _later));
        DomainRuleException updateMissing = Assert.Throws<DomainRuleException>(
            () => request.UpdateItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaac"),
                "Missing",
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Other,
                _later));
        DomainRuleException removeMissing = Assert.Throws<DomainRuleException>(
            () => request.RemoveItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaac"),
                _later));

        Assert.Equal(DomainErrorCodes.DuplicateEntity, duplicate.Code);
        Assert.Equal(DomainErrorCodes.EntityNotFound, updateMissing.Code);
        Assert.Equal(DomainErrorCodes.EntityNotFound, removeMissing.Code);
        Assert.Single(request.Items);
        Assert.Equal(2_500m, request.Total.Amount);
    }

    [Fact]
    public void ForeignCurrencyIsRejectedWithoutMutation()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.ClearDomainEvents();
        Money usd = Money.Create(10m, CurrencyCode.Parse("USD"));

        DomainRuleException add = Assert.Throws<DomainRuleException>(
            () => request.AddItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaad"),
                "Foreign",
                1,
                usd,
                ProcurementCategory.Other,
                _later));
        DomainRuleException update = Assert.Throws<DomainRuleException>(
            () => request.UpdateItem(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Foreign",
                1,
                usd,
                ProcurementCategory.Other,
                _later));

        Assert.Equal(DomainErrorCodes.CurrencyMismatch, add.Code);
        Assert.Equal(DomainErrorCodes.CurrencyMismatch, update.Code);
        Assert.Single(request.Items);
        Assert.Equal(2_500m, request.Total.Amount);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void AggregateTotalOverflowDoesNotPartiallyAddOrChangeItem()
    {
        Guid secondId = Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaae");
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithItem(quantity: 1, unitPrice: Money.MaximumAmount)
            .Build();
        request.ClearDomainEvents();

        DomainRuleException addOverflow = Assert.Throws<DomainRuleException>(
            () => request.AddItem(
                secondId,
                "Extra",
                1,
                Money.Create(0.01m, _inr),
                ProcurementCategory.Other,
                _later));

        Assert.Equal(DomainErrorCodes.AmountOverflow, addOverflow.Code);
        Assert.Single(request.Items);
        Assert.Equal(Money.MaximumAmount, request.Total.Amount);
        Assert.Empty(request.DomainEvents);

        PurchaseRequest twoItems = new PurchaseRequestBuilder()
            .WithItem(quantity: 1, unitPrice: Money.MaximumAmount - 0.01m)
            .WithItem(secondId, quantity: 1, unitPrice: 0.01m)
            .Build();
        twoItems.ClearDomainEvents();

        DomainRuleException updateOverflow = Assert.Throws<DomainRuleException>(
            () => twoItems.UpdateItem(
                secondId,
                "Extra",
                1,
                Money.Create(0.02m, _inr),
                ProcurementCategory.Other,
                _later));

        Assert.Equal(DomainErrorCodes.AmountOverflow, updateOverflow.Code);
        Assert.Equal(0.01m, twoItems.Items.Single(item => item.Id == secondId).UnitPrice.Amount);
        Assert.Equal(Money.MaximumAmount, twoItems.Total.Amount);
        Assert.Empty(twoItems.DomainEvents);
    }

    [Fact]
    public void EarlierOrNonUtcMutationTimeCannotPartiallyMutateAggregate()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();
        request.ClearDomainEvents();

        Assert.Throws<DomainRuleException>(
            () => request.AddItem(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Laptop",
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Hardware,
                PurchaseRequestBuilder.DefaultTime.AddTicks(-1)));
        Assert.Throws<DomainRuleException>(
            () => request.UpdateMetadata(
                PurchaseRequestBuilder.DefaultMetadata(),
                PurchaseRequestBuilder.DefaultTime.ToOffset(TimeSpan.FromHours(1))));

        Assert.Empty(request.Items);
        Assert.Equal(0m, request.Total.Amount);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void SubmittedRequestRejectsEveryDraftMutation()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(_later);

        AssertInvalidState(
            () => request.UpdateMetadata(PurchaseRequestBuilder.DefaultMetadata(), _later));
        AssertInvalidState(
            () => request.AddItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaaf"),
                "Extra",
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Other,
                _later));
        AssertInvalidState(
            () => request.UpdateItem(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Changed",
                1,
                Money.Create(1m, _inr),
                ProcurementCategory.Other,
                _later));
        AssertInvalidState(
            () => request.RemoveItem(PurchaseRequestItemBuilder.DefaultItemId, _later));
    }

    private static void AssertInvalidState(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.InvalidState, exception.Code);
    }
}
