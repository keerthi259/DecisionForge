using DecisionForge.Domain.Common;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed partial class PurchaseRequest
{
    public PurchaseRequest Clone(
        Guid id,
        RequestNumber requestNumber,
        Guid requesterId,
        IReadOnlyList<Guid> itemIds,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        if (requesterId != RequesterId)
        {
            throw new DomainRuleException(
                DomainErrorCodes.ReferenceMismatch,
                "A purchase request can only be cloned for its owning requester.",
                nameof(requesterId));
        }

        ValidateCloneItemIds(itemIds);
        PurchaseRequest clone = Create(
            id,
            requestNumber,
            requesterId,
            Currency,
            Metadata,
            concurrencyToken,
            createdAt);
        for (int index = 0; index < _items.Count; index++)
        {
            PurchaseRequestItem source = _items[index];
            PurchaseRequestItem item = PurchaseRequestItem.Create(
                itemIds[index],
                source.Description,
                source.Quantity,
                source.UnitPrice,
                source.Category);
            clone._items.Add(item);
            clone.Total = clone.Total.Add(item.LineTotal);
        }

        clone.Raise(new PurchaseRequestClonedDomainEvent(
            clone.Id,
            Id,
            requesterId,
            clone.CreatedAt));
        return clone;
    }

    private void ValidateCloneItemIds(IReadOnlyList<Guid> itemIds)
    {
        if (itemIds.Count != _items.Count
            || itemIds.Any(itemId => itemId == Guid.Empty)
            || itemIds.Distinct().Count() != itemIds.Count)
        {
            throw DomainGuard.Validation(
                nameof(itemIds),
                "Clone item identities must be non-empty, unique and match the source item count.");
        }
    }
}
