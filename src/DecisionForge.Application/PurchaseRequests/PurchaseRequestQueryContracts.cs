using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.PurchaseRequests;

public enum PurchaseRequestSortOrder
{
    CreatedAtDescending = 1,
    CreatedAtAscending = 2,
    RequestNumberAscending = 3,
}

public sealed record ListPurchaseRequestsQuery(
    int Offset,
    int PageSize,
    PurchaseRequestStatus? Status,
    PurchaseRequestSortOrder SortOrder);

public sealed record GetPurchaseRequestDetailQuery(Guid PurchaseRequestId);

public sealed class PurchaseRequestSummary
{
    public PurchaseRequestSummary(
        Guid id,
        RequestNumber requestNumber,
        PurchaseRequestStatus status,
        Money total,
        DateTimeOffset createdAt,
        DateTimeOffset? submittedAt,
        ConcurrencyToken concurrencyToken)
    {
        Id = id;
        RequestNumber = requestNumber;
        Status = status;
        Total = total;
        CreatedAt = createdAt;
        SubmittedAt = submittedAt;
        ConcurrencyToken = concurrencyToken;
    }

    public Guid Id { get; }

    public RequestNumber RequestNumber { get; }

    public PurchaseRequestStatus Status { get; }

    public Money Total { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? SubmittedAt { get; }

    public ConcurrencyToken ConcurrencyToken { get; }
}

public sealed class PurchaseRequestItemDetail
{
    public PurchaseRequestItemDetail(
        Guid id,
        string description,
        int quantity,
        Money unitPrice,
        Money lineTotal,
        ProcurementCategory category)
    {
        Id = id;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = lineTotal;
        Category = category;
    }

    public Guid Id { get; }

    public string Description { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money LineTotal { get; }

    public ProcurementCategory Category { get; }
}

public sealed class PurchaseRequestDetail
{
    private readonly ReadOnlyCollection<PurchaseRequestItemDetail> _items;

    public PurchaseRequestDetail(
        Guid id,
        RequestNumber requestNumber,
        PurchaseRequestStatus status,
        CurrencyCode currency,
        PurchaseRequestMetadata metadata,
        Money total,
        IReadOnlyCollection<PurchaseRequestItemDetail> items,
        DateTimeOffset createdAt,
        DateTimeOffset lastModifiedAt,
        DateTimeOffset? submittedAt,
        ConcurrencyToken concurrencyToken)
    {
        ArgumentNullException.ThrowIfNull(requestNumber);
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(total);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(concurrencyToken);
        Id = id;
        RequestNumber = requestNumber;
        Status = status;
        Currency = currency;
        Metadata = metadata;
        Total = total;
        _items = Array.AsReadOnly(items.ToArray());
        CreatedAt = createdAt;
        LastModifiedAt = lastModifiedAt;
        SubmittedAt = submittedAt;
        ConcurrencyToken = concurrencyToken;
    }

    public Guid Id { get; }

    public RequestNumber RequestNumber { get; }

    public PurchaseRequestStatus Status { get; }

    public CurrencyCode Currency { get; }

    public PurchaseRequestMetadata Metadata { get; }

    public Money Total { get; }

    public IReadOnlyList<PurchaseRequestItemDetail> Items => _items;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; }

    public DateTimeOffset? SubmittedAt { get; }

    public ConcurrencyToken ConcurrencyToken { get; }
}

public sealed class PurchaseRequestPage
{
    public const int MaximumPageSize = 100;

    private PurchaseRequestPage(
        int offset,
        int pageSize,
        PurchaseRequestStatus? status,
        PurchaseRequestSortOrder sortOrder)
    {
        Offset = offset;
        PageSize = pageSize;
        Status = status;
        SortOrder = sortOrder;
    }

    public int Offset { get; }

    public int PageSize { get; }

    public PurchaseRequestStatus? Status { get; }

    public PurchaseRequestSortOrder SortOrder { get; }

    public static PurchaseRequestPage Create(
        int offset,
        int pageSize,
        PurchaseRequestStatus? status,
        PurchaseRequestSortOrder sortOrder)
    {
        if (offset < 0)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Purchase-request offset must not be negative.",
                nameof(offset));
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                $"Purchase-request page size must be between 1 and {MaximumPageSize}.",
                nameof(pageSize));
        }

        if (status is not null && !Enum.IsDefined(status.Value))
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Purchase-request status filter is not supported.",
                nameof(status));
        }

        if (!Enum.IsDefined(sortOrder))
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Purchase-request sort order is not supported.",
                nameof(sortOrder));
        }

        return new PurchaseRequestPage(offset, pageSize, status, sortOrder);
    }
}

public sealed class PurchaseRequestListResult
{
    private readonly ReadOnlyCollection<PurchaseRequestSummary> _items;

    public PurchaseRequestListResult(
        IReadOnlyCollection<PurchaseRequestSummary> items,
        int totalCount,
        int offset,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (totalCount < 0 || offset < 0 || pageSize is < 1 or > PurchaseRequestPage.MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount));
        }

        if (items.Count > pageSize || totalCount < items.Count)
        {
            throw new ArgumentException("Page contents must be bounded by the declared result counts.", nameof(items));
        }

        _items = Array.AsReadOnly(items.ToArray());
        TotalCount = totalCount;
        Offset = offset;
        PageSize = pageSize;
    }

    public IReadOnlyList<PurchaseRequestSummary> Items => _items;

    public int TotalCount { get; }

    public int Offset { get; }

    public int PageSize { get; }
}
