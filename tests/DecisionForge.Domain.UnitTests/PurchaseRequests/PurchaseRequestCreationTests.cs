using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestCreationTests
{
    [Fact]
    public void CreateProducesDeterministicOwnedDraft()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();

        Assert.Equal(PurchaseRequestBuilder.DefaultRequestId, request.Id);
        Assert.Equal(PurchaseRequestBuilder.DefaultRequesterId, request.RequesterId);
        Assert.Equal("PR-2026-000001", request.RequestNumber.Value);
        Assert.Equal("INR", request.Currency.Value);
        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Equal(Money.Zero(request.Currency), request.Total);
        Assert.Equal(PurchaseRequestBuilder.Token(0), request.ConcurrencyToken);
        Assert.Empty(request.Items);
        ICollection<PurchaseRequestItem> exposedItems =
            Assert.IsAssignableFrom<ICollection<PurchaseRequestItem>>(request.Items);
        Assert.True(exposedItems.IsReadOnly);
        Assert.Throws<NotSupportedException>(
            () => exposedItems.Add(new PurchaseRequestItemBuilder().Build()));
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, request.CreatedAt);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, request.LastModifiedAt);
        Assert.Null(request.SubmittedAt);

        PurchaseRequestCreatedDomainEvent created =
            Assert.IsType<PurchaseRequestCreatedDomainEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(request.Id, created.PurchaseRequestId);
        Assert.Equal(request.RequesterId, created.RequesterId);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, created.OccurredAt);
    }

    [Fact]
    public void CreateRejectsInvalidIdentityNullValuesAndNonUtcTime()
    {
        PurchaseRequestMetadata metadata = PurchaseRequestBuilder.DefaultMetadata();
        CurrencyCode currency = CurrencyCode.Parse("INR");
        RequestNumber number = RequestNumber.Parse("PR-1");

        Assert.Throws<DomainRuleException>(
            () => PurchaseRequest.Create(
                Guid.Empty,
                number,
                PurchaseRequestBuilder.DefaultRequesterId,
                currency,
                metadata,
                PurchaseRequestBuilder.Token(0),
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(
            () => new PurchaseRequestBuilder().WithRequester(Guid.Empty).Build());
        Assert.Throws<ArgumentNullException>(
            () => PurchaseRequest.Create(
                PurchaseRequestBuilder.DefaultRequestId,
                null!,
                PurchaseRequestBuilder.DefaultRequesterId,
                currency,
                metadata,
                PurchaseRequestBuilder.Token(0),
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<ArgumentNullException>(
            () => PurchaseRequest.Create(
                PurchaseRequestBuilder.DefaultRequestId,
                number,
                PurchaseRequestBuilder.DefaultRequesterId,
                null!,
                metadata,
                PurchaseRequestBuilder.Token(0),
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<ArgumentNullException>(
            () => PurchaseRequest.Create(
                PurchaseRequestBuilder.DefaultRequestId,
                number,
                PurchaseRequestBuilder.DefaultRequesterId,
                currency,
                null!,
                PurchaseRequestBuilder.Token(0),
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<ArgumentNullException>(
            () => PurchaseRequest.Create(
                PurchaseRequestBuilder.DefaultRequestId,
                number,
                PurchaseRequestBuilder.DefaultRequesterId,
                currency,
                metadata,
                null!,
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequest.Create(
                PurchaseRequestBuilder.DefaultRequestId,
                number,
                PurchaseRequestBuilder.DefaultRequesterId,
                currency,
                metadata,
                PurchaseRequestBuilder.Token(0),
                PurchaseRequestBuilder.DefaultTime.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void MetadataValidatesReferencesAndEnums()
    {
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestMetadata.Create(
                Guid.Empty,
                PurchaseRequestBuilder.DefaultSupplierId,
                Urgency.Normal,
                DataSensitivity.Internal,
                new DateOnly(2026, 8, 31),
                null));
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestMetadata.Create(
                PurchaseRequestBuilder.DefaultDepartmentId,
                PurchaseRequestBuilder.DefaultSupplierId,
                Urgency.Normal,
                DataSensitivity.Internal,
                default,
                null));
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestMetadata.Create(
                PurchaseRequestBuilder.DefaultDepartmentId,
                Guid.Empty,
                Urgency.Normal,
                DataSensitivity.Internal,
                new DateOnly(2026, 8, 31),
                null));
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestMetadata.Create(
                PurchaseRequestBuilder.DefaultDepartmentId,
                PurchaseRequestBuilder.DefaultSupplierId,
                (Urgency)999,
                DataSensitivity.Internal,
                new DateOnly(2026, 8, 31),
                null));
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestMetadata.Create(
                PurchaseRequestBuilder.DefaultDepartmentId,
                PurchaseRequestBuilder.DefaultSupplierId,
                Urgency.Normal,
                (DataSensitivity)999,
                new DateOnly(2026, 8, 31),
                null));
    }
}
