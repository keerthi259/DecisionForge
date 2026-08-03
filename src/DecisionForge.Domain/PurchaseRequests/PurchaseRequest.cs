using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed partial class PurchaseRequest : AggregateRoot
{
    private readonly List<PurchaseRequestItem> _items = [];
    private readonly ReadOnlyCollection<PurchaseRequestItem> _itemsView;

    private PurchaseRequest(
        Guid id,
        RequestNumber requestNumber,
        Guid requesterId,
        CurrencyCode currency,
        PurchaseRequestMetadata metadata,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
        : base(id)
    {
        _itemsView = _items.AsReadOnly();
        RequestNumber = requestNumber;
        RequesterId = requesterId;
        Currency = currency;
        Metadata = metadata;
        ConcurrencyToken = concurrencyToken;
        Status = PurchaseRequestStatus.Draft;
        Total = Money.Zero(currency);
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
    }

    public RequestNumber RequestNumber { get; }

    public Guid RequesterId { get; }

    public CurrencyCode Currency { get; }

    public PurchaseRequestMetadata Metadata { get; private set; }

    public PurchaseRequestStatus Status { get; private set; }

    public Money Total { get; private set; }

    public ConcurrencyToken ConcurrencyToken { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public PurchaseRequestEvaluationContext? EvaluationContext { get; private set; }

    public IReadOnlyList<PurchaseRequestItem> Items => _itemsView;

    public static PurchaseRequest Create(
        Guid id,
        RequestNumber requestNumber,
        Guid requesterId,
        CurrencyCode currency,
        PurchaseRequestMetadata metadata,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(requestNumber);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(concurrencyToken);
        DomainGuard.NotEmpty(requesterId, nameof(requesterId));
        DateTimeOffset utcCreatedAt = DomainGuard.Utc(createdAt, nameof(createdAt));

        PurchaseRequest request = new(
            id,
            requestNumber,
            requesterId,
            currency,
            metadata,
            concurrencyToken,
            utcCreatedAt);
        request.Raise(new PurchaseRequestCreatedDomainEvent(id, requesterId, utcCreatedAt));
        return request;
    }

    public void UpdateMetadata(
        PurchaseRequestMetadata metadata,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        if (Metadata == metadata)
        {
            return;
        }

        Metadata = metadata;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestMetadataChangedDomainEvent(
            Id,
            metadata.DepartmentId,
            metadata.SupplierId,
            utcOccurredAt));
    }

    public PurchaseRequestItem AddItem(
        Guid itemId,
        string description,
        int quantity,
        Money unitPrice,
        ProcurementCategory category,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        if (_items.Any(item => item.Id == itemId))
        {
            throw new DomainRuleException(
                DomainErrorCodes.DuplicateEntity,
                $"Purchase request item '{itemId}' already exists.",
                nameof(itemId));
        }

        EnsureCurrency(unitPrice);
        PurchaseRequestItem item = PurchaseRequestItem.Create(
            itemId,
            description,
            quantity,
            unitPrice,
            category);
        Money newTotal = Total.Add(item.LineTotal);
        _items.Add(item);
        Total = newTotal;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestItemAddedDomainEvent(
            Id,
            item.Id,
            item.Quantity,
            item.UnitPrice,
            utcOccurredAt));
        return item;
    }

    public void UpdateItem(
        Guid itemId,
        string description,
        int quantity,
        Money unitPrice,
        ProcurementCategory category,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        PurchaseRequestItem item = FindItem(itemId);
        EnsureCurrency(unitPrice);
        PurchaseRequestItem proposed = PurchaseRequestItem.Create(
            itemId,
            description,
            quantity,
            unitPrice,
            category);
        if (item.Description == proposed.Description
            && item.Quantity == proposed.Quantity
            && item.UnitPrice == proposed.UnitPrice
            && item.Category == proposed.Category)
        {
            return;
        }

        Money newTotal = CalculateTotal(itemId, proposed);
        _ = item.Change(description, quantity, unitPrice, category);
        Total = newTotal;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestItemChangedDomainEvent(
            Id,
            item.Id,
            item.Quantity,
            item.UnitPrice,
            utcOccurredAt));
    }

    public void RemoveItem(
        Guid itemId,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        PurchaseRequestItem item = FindItem(itemId);
        _items.Remove(item);
        Total = CalculateTotal();
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestItemRemovedDomainEvent(Id, item.Id, utcOccurredAt));
    }

    private PurchaseRequestItem FindItem(Guid itemId)
    {
        PurchaseRequestItem? item = _items.SingleOrDefault(candidate => candidate.Id == itemId);
        if (item is null)
        {
            throw new DomainRuleException(
                DomainErrorCodes.EntityNotFound,
                $"Purchase request item '{itemId}' was not found.",
                nameof(itemId));
        }

        return item;
    }

    private void EnsureCurrency(Money unitPrice)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);
        if (unitPrice.Currency != Currency)
        {
            throw new DomainRuleException(
                DomainErrorCodes.CurrencyMismatch,
                "Item currency must match the purchase request currency.",
                nameof(unitPrice));
        }
    }

    private void EnsureDraft()
    {
        EnsureStatus(PurchaseRequestStatus.Draft);
    }

    private void EnsureStatus(params PurchaseRequestStatus[] allowedStatuses)
    {
        if (!allowedStatuses.Contains(Status))
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                $"Purchase request in '{Status}' state does not allow this operation.");
        }
    }

    private Money CalculateTotal(
        Guid replacedItemId = default,
        PurchaseRequestItem? replacement = null)
    {
        return _items.Aggregate(
            Money.Zero(Currency),
            (current, item) => current.Add(
                item.Id == replacedItemId && replacement is not null
                    ? replacement.LineTotal
                    : item.LineTotal));
    }

    private DateTimeOffset ValidateMutation(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        return PurchaseRequestGuard.Mutation(
            ConcurrencyToken,
            expectedToken,
            nextToken,
            LastModifiedAt,
            occurredAt);
    }

    private void CompleteMutation(ConcurrencyToken nextToken, DateTimeOffset occurredAt)
    {
        ConcurrencyToken = nextToken;
        LastModifiedAt = occurredAt;
    }
}
