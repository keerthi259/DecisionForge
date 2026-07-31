using DecisionForge.Domain.Common;
using DecisionForge.Domain.ReferenceData.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.ReferenceData;

public sealed class Department : AggregateRoot
{
    private Department(
        Guid id,
        DepartmentCode code,
        string name,
        Money autoApprovalLimit,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        AutoApprovalLimit = autoApprovalLimit;
        ConcurrencyToken = concurrencyToken;
        IsActive = true;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
    }

    public DepartmentCode Code { get; }

    public string Name { get; private set; }

    public Money AutoApprovalLimit { get; private set; }

    public bool IsActive { get; private set; }

    public ConcurrencyToken ConcurrencyToken { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public static Department Create(
        Guid id,
        DepartmentCode code,
        string name,
        Money autoApprovalLimit,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(autoApprovalLimit);
        ArgumentNullException.ThrowIfNull(concurrencyToken);
        string normalizedName = ReferenceDataGuard.Name(name);
        DateTimeOffset utcCreatedAt = DomainGuard.Utc(createdAt, nameof(createdAt));

        Department department = new(
            id,
            code,
            normalizedName,
            autoApprovalLimit,
            concurrencyToken,
            utcCreatedAt);
        department.Raise(new DepartmentCreatedDomainEvent(id, code, utcCreatedAt));
        return department;
    }

    public void UpdateDetails(
        string name,
        Money autoApprovalLimit,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(autoApprovalLimit);
        string normalizedName = ReferenceDataGuard.Name(name);
        DateTimeOffset utcOccurredAt = ReferenceDataGuard.Mutation(
            ConcurrencyToken,
            expectedToken,
            nextToken,
            LastModifiedAt,
            occurredAt);
        if (Name == normalizedName && AutoApprovalLimit == autoApprovalLimit)
        {
            return;
        }

        Name = normalizedName;
        AutoApprovalLimit = autoApprovalLimit;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new DepartmentDetailsChangedDomainEvent(Id, autoApprovalLimit, utcOccurredAt));
    }

    public void SetActive(
        bool isActive,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = ReferenceDataGuard.Mutation(
            ConcurrencyToken,
            expectedToken,
            nextToken,
            LastModifiedAt,
            occurredAt);
        if (IsActive == isActive)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                $"Department is already {(isActive ? "active" : "inactive")}.");
        }

        IsActive = isActive;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new DepartmentActivationChangedDomainEvent(Id, isActive, utcOccurredAt));
    }

    private void CompleteMutation(ConcurrencyToken nextToken, DateTimeOffset occurredAt)
    {
        ConcurrencyToken = nextToken;
        LastModifiedAt = occurredAt;
    }
}
