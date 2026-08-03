using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Approvals;

public enum ApprovalInboxSortOrder
{
    CreatedAtDescending = 1,
    CreatedAtAscending = 2,
}

public sealed record ListApprovalInboxQuery(
    int Offset,
    int PageSize,
    PolicyApproverRole? RequiredRole,
    ApprovalInboxSortOrder SortOrder);

public sealed record GetApprovalWorkflowDetailQuery(Guid WorkflowId);

public sealed class ApprovalInboxPage
{
    public const int MaximumPageSize = 100;

    private ApprovalInboxPage(int offset, int pageSize, ApprovalInboxSortOrder sortOrder)
    {
        Offset = offset;
        PageSize = pageSize;
        SortOrder = sortOrder;
    }

    public int Offset { get; }

    public int PageSize { get; }

    public ApprovalInboxSortOrder SortOrder { get; }

    public static ApprovalInboxPage Create(
        int offset,
        int pageSize,
        ApprovalInboxSortOrder sortOrder)
    {
        if (offset < 0)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Approval inbox offset must not be negative.",
                nameof(offset));
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                $"Approval inbox page size must be between 1 and {MaximumPageSize}.",
                nameof(pageSize));
        }

        if (!Enum.IsDefined(sortOrder))
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Approval inbox sort order is not supported.",
                nameof(sortOrder));
        }

        return new ApprovalInboxPage(offset, pageSize, sortOrder);
    }
}

public sealed record ApprovalInboxItem(
    Guid WorkflowId,
    Guid StageId,
    Guid PurchaseRequestId,
    RequestNumber RequestNumber,
    PolicyApproverRole RequiredRole,
    DateTimeOffset CreatedAt,
    ConcurrencyToken ConcurrencyToken);

public sealed class ApprovalInboxResult
{
    private readonly ReadOnlyCollection<ApprovalInboxItem> _items;

    public ApprovalInboxResult(
        IReadOnlyCollection<ApprovalInboxItem> items,
        int totalCount,
        int offset,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (totalCount < 0
            || offset < 0
            || pageSize is < 1 or > ApprovalInboxPage.MaximumPageSize
            || items.Count > pageSize
            || totalCount < items.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount));
        }

        _items = Array.AsReadOnly(items.ToArray());
        TotalCount = totalCount;
        Offset = offset;
        PageSize = pageSize;
    }

    public IReadOnlyList<ApprovalInboxItem> Items => _items;

    public int TotalCount { get; }

    public int Offset { get; }

    public int PageSize { get; }
}

public sealed record ApprovalStageDetail(
    Guid Id,
    int Sequence,
    PolicyApproverRole RequiredRole,
    ApprovalStageStatus Status,
    Guid? ActorId,
    string? Note,
    DateTimeOffset? ActedAt,
    ConcurrencyToken ConcurrencyToken);

public sealed record ApprovalOverrideDetail(
    DecisionDisposition OriginalDisposition,
    ApprovalOutcome Outcome,
    Guid ActorId,
    string Reason,
    DateTimeOffset OccurredAt);

public sealed class ApprovalWorkflowDetail
{
    private readonly ReadOnlyCollection<ApprovalStageDetail> _stages;

    public ApprovalWorkflowDetail(
        Guid id,
        Guid purchaseRequestId,
        Guid decisionId,
        RequestNumber requestNumber,
        DecisionDisposition originalDisposition,
        ApprovalWorkflowStatus status,
        IReadOnlyCollection<ApprovalStageDetail> stages,
        ApprovalOverrideDetail? approvalOverride,
        DateTimeOffset createdAt,
        DateTimeOffset? completedAt)
    {
        ArgumentNullException.ThrowIfNull(requestNumber);
        ArgumentNullException.ThrowIfNull(stages);
        Id = id;
        PurchaseRequestId = purchaseRequestId;
        DecisionId = decisionId;
        RequestNumber = requestNumber;
        OriginalDisposition = originalDisposition;
        Status = status;
        _stages = Array.AsReadOnly(stages.OrderBy(stage => stage.Sequence).ToArray());
        Override = approvalOverride;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
    }

    public Guid Id { get; }

    public Guid PurchaseRequestId { get; }

    public Guid DecisionId { get; }

    public RequestNumber RequestNumber { get; }

    public DecisionDisposition OriginalDisposition { get; }

    public ApprovalWorkflowStatus Status { get; }

    public IReadOnlyList<ApprovalStageDetail> Stages => _stages;

    public ApprovalOverrideDetail? Override { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? CompletedAt { get; }
}
