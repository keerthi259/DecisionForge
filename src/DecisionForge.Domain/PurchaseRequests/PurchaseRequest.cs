using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed class PurchaseRequest : AggregateRoot
{
    private readonly List<PurchaseRequestItem> _items = [];
    private readonly ReadOnlyCollection<PurchaseRequestItem> _itemsView;

    private PurchaseRequest(
        Guid id,
        RequestNumber requestNumber,
        Guid requesterId,
        CurrencyCode currency,
        PurchaseRequestMetadata metadata,
        DateTimeOffset createdAt)
        : base(id)
    {
        _itemsView = _items.AsReadOnly();
        RequestNumber = requestNumber;
        RequesterId = requesterId;
        Currency = currency;
        Metadata = metadata;
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

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public IReadOnlyList<PurchaseRequestItem> Items => _itemsView;

    public static PurchaseRequest Create(
        Guid id,
        RequestNumber requestNumber,
        Guid requesterId,
        CurrencyCode currency,
        PurchaseRequestMetadata metadata,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(requestNumber);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(metadata);
        DomainGuard.NotEmpty(requesterId, nameof(requesterId));
        DateTimeOffset utcCreatedAt = DomainGuard.Utc(createdAt, nameof(createdAt));

        PurchaseRequest request = new(
            id,
            requestNumber,
            requesterId,
            currency,
            metadata,
            utcCreatedAt);
        request.Raise(new PurchaseRequestCreatedDomainEvent(id, requesterId, utcCreatedAt));
        return request;
    }

    public void UpdateMetadata(PurchaseRequestMetadata metadata, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        if (Metadata == metadata)
        {
            return;
        }

        Metadata = metadata;
        Touch(utcOccurredAt);
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
        DateTimeOffset occurredAt)
    {
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
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
        Touch(utcOccurredAt);
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
        DateTimeOffset occurredAt)
    {
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
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
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestItemChangedDomainEvent(
            Id,
            item.Id,
            item.Quantity,
            item.UnitPrice,
            utcOccurredAt));
    }

    public void RemoveItem(Guid itemId, DateTimeOffset occurredAt)
    {
        EnsureDraft();
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        PurchaseRequestItem item = FindItem(itemId);
        _items.Remove(item);
        Total = CalculateTotal();
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestItemRemovedDomainEvent(Id, item.Id, utcOccurredAt));
    }

    public void Submit(DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.Draft);
        if (_items.Count == 0)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                "A purchase request requires at least one item before submission.");
        }

        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        Status = PurchaseRequestStatus.Submitted;
        SubmittedAt = utcOccurredAt;
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestSubmittedDomainEvent(Id, Total, utcOccurredAt));
    }

    public void BeginEvaluation(DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.Submitted);
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        Status = PurchaseRequestStatus.Evaluating;
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestEvaluationStartedDomainEvent(Id, utcOccurredAt));
    }

    public void MarkEvaluationFailed(ReasonCode reasonCode, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reasonCode);
        EnsureStatus(PurchaseRequestStatus.Evaluating);
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        Status = PurchaseRequestStatus.EvaluationFailed;
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestEvaluationFailedDomainEvent(Id, reasonCode, utcOccurredAt));
    }

    public void RetryEvaluation(DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.EvaluationFailed);
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        Status = PurchaseRequestStatus.Submitted;
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestEvaluationRetriedDomainEvent(Id, utcOccurredAt));
    }

    public void Withdraw(DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.Submitted, PurchaseRequestStatus.PendingApproval);
        DateTimeOffset utcOccurredAt = ValidateMutationTime(occurredAt);
        Status = PurchaseRequestStatus.Withdrawn;
        Touch(utcOccurredAt);
        Raise(new PurchaseRequestWithdrawnDomainEvent(Id, utcOccurredAt));
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

    private DateTimeOffset ValidateMutationTime(DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = DomainGuard.Utc(occurredAt, nameof(occurredAt));
        if (utcOccurredAt < LastModifiedAt)
        {
            throw DomainGuard.Validation(
                nameof(occurredAt),
                "Mutation time cannot precede the previous aggregate change.");
        }

        return utcOccurredAt;
    }

    private void Touch(DateTimeOffset occurredAt)
    {
        LastModifiedAt = occurredAt;
    }
}
