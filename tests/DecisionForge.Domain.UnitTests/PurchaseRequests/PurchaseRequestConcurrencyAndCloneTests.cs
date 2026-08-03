using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestConcurrencyAndCloneTests
{
    private static readonly DateTimeOffset _later = PurchaseRequestBuilder.DefaultTime.AddMinutes(1);

    [Fact]
    public void SuccessfulMutationRotatesTokenAndStaleMutationIsAtomic()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();
        ConcurrencyToken initial = request.ConcurrencyToken;
        ConcurrencyToken next = PurchaseRequestBuilder.Token(10);

        _ = request.AddItem(
            PurchaseRequestItemBuilder.DefaultItemId,
            "Laptop",
            1,
            Money.Create(100m, request.Currency),
            ProcurementCategory.Hardware,
            initial,
            next,
            _later);
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => request.RemoveItem(
                PurchaseRequestItemBuilder.DefaultItemId,
                initial,
                PurchaseRequestBuilder.Token(11),
                _later));

        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, exception.Code);
        Assert.Equal(next, request.ConcurrencyToken);
        Assert.Single(request.Items);
        Assert.Equal(100m, request.Total.Amount);
    }

    [Fact]
    public void ReusedNextTokenIsRejectedWithoutMutation()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => request.AddItem(
                PurchaseRequestItemBuilder.DefaultItemId,
                "Laptop",
                1,
                Money.Create(100m, request.Currency),
                ProcurementCategory.Hardware,
                request.ConcurrencyToken,
                request.ConcurrencyToken,
                _later));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Empty(request.Items);
    }

    [Fact]
    public void NoOpMutationPreservesConcurrencyTokenAndTimestamp()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();
        ConcurrencyToken initial = request.ConcurrencyToken;

        request.UpdateMetadata(
            request.Metadata,
            initial,
            PurchaseRequestBuilder.Token(10),
            _later);

        Assert.Equal(initial, request.ConcurrencyToken);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, request.LastModifiedAt);
    }

    [Fact]
    public void CloneCreatesIndependentDraftWithAuthoritativeTotalAndNewIdentities()
    {
        PurchaseRequest source = new PurchaseRequestBuilder()
            .WithItem(quantity: 2, unitPrice: 1_250m)
            .Build();
        source.Submit(
            source.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _later);
        source.ClearDomainEvents();
        Guid cloneId = Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaa1");
        Guid cloneItemId = Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaa2");
        DateTimeOffset clonedAt = _later.AddMinutes(1);

        PurchaseRequest clone = source.Clone(
            cloneId,
            RequestNumber.Parse("PR-2026-000002"),
            source.RequesterId,
            [cloneItemId],
            PurchaseRequestBuilder.Token(20),
            clonedAt);

        Assert.Equal(PurchaseRequestStatus.Draft, clone.Status);
        Assert.Null(clone.SubmittedAt);
        Assert.Equal(source.Metadata, clone.Metadata);
        Assert.Equal(source.Total, clone.Total);
        Assert.Equal(source.Currency, clone.Currency);
        Assert.Equal(PurchaseRequestBuilder.Token(20), clone.ConcurrencyToken);
        PurchaseRequestItem clonedItem = Assert.Single(clone.Items);
        Assert.Equal(cloneItemId, clonedItem.Id);
        Assert.NotEqual(source.Items[0].Id, clonedItem.Id);
        Assert.Equal(source.Items[0].LineTotal, clonedItem.LineTotal);
        Assert.Empty(source.DomainEvents);
        Assert.Collection(
            clone.DomainEvents,
            domainEvent => Assert.IsType<PurchaseRequestCreatedDomainEvent>(domainEvent),
            domainEvent =>
            {
                PurchaseRequestClonedDomainEvent cloned =
                    Assert.IsType<PurchaseRequestClonedDomainEvent>(domainEvent);
                Assert.Equal(source.Id, cloned.SourcePurchaseRequestId);
                Assert.Equal(cloneId, cloned.PurchaseRequestId);
            });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CloneRejectsWrongOwnerOrInvalidItemIdentity(bool wrongOwner, bool emptyItemId)
    {
        PurchaseRequest source = new PurchaseRequestBuilder().WithItem().Build();
        Guid requesterId = wrongOwner
            ? Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaa3")
            : source.RequesterId;
        Guid itemId = emptyItemId ? Guid.Empty : Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaa2");

        Assert.Throws<DomainRuleException>(
            () => source.Clone(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaaa1"),
                RequestNumber.Parse("PR-2026-000002"),
                requesterId,
                [itemId],
                PurchaseRequestBuilder.Token(20),
                _later));
    }
}
