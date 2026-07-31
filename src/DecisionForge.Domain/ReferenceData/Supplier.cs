using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ReferenceData.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.ReferenceData;

public sealed class Supplier : AggregateRoot
{
    private Supplier(
        Guid id,
        SupplierRegistrationNumber registrationNumber,
        string name,
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
        : base(id)
    {
        RegistrationNumber = registrationNumber;
        Name = name;
        ApprovalStatus = approvalStatus;
        OnboardingStatus = onboardingStatus;
        RiskRating = riskRating;
        ConcurrencyToken = concurrencyToken;
        IsActive = true;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
    }

    public SupplierRegistrationNumber RegistrationNumber { get; }

    public string Name { get; private set; }

    public SupplierApprovalStatus ApprovalStatus { get; private set; }

    public SupplierOnboardingStatus OnboardingStatus { get; private set; }

    public SupplierRiskRating RiskRating { get; private set; }

    public bool IsActive { get; private set; }

    public ConcurrencyToken ConcurrencyToken { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public static Supplier Create(
        Guid id,
        SupplierRegistrationNumber registrationNumber,
        string name,
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(registrationNumber);
        ArgumentNullException.ThrowIfNull(concurrencyToken);
        ValidateStates(approvalStatus, onboardingStatus, riskRating);
        string normalizedName = ReferenceDataGuard.Name(name);
        DateTimeOffset utcCreatedAt = DomainGuard.Utc(createdAt, nameof(createdAt));

        Supplier supplier = new(
            id,
            registrationNumber,
            normalizedName,
            approvalStatus,
            onboardingStatus,
            riskRating,
            concurrencyToken,
            utcCreatedAt);
        supplier.Raise(new SupplierCreatedDomainEvent(id, registrationNumber, utcCreatedAt));
        return supplier;
    }

    public void UpdateDetails(
        string name,
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ValidateStates(approvalStatus, onboardingStatus, riskRating);
        string normalizedName = ReferenceDataGuard.Name(name);
        DateTimeOffset utcOccurredAt = ReferenceDataGuard.Mutation(
            ConcurrencyToken,
            expectedToken,
            nextToken,
            LastModifiedAt,
            occurredAt);
        if (Name == normalizedName
            && ApprovalStatus == approvalStatus
            && OnboardingStatus == onboardingStatus
            && RiskRating == riskRating)
        {
            return;
        }

        Name = normalizedName;
        ApprovalStatus = approvalStatus;
        OnboardingStatus = onboardingStatus;
        RiskRating = riskRating;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new SupplierDetailsChangedDomainEvent(
            Id,
            approvalStatus,
            onboardingStatus,
            riskRating,
            utcOccurredAt));
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
                $"Supplier is already {(isActive ? "active" : "inactive")}.");
        }

        IsActive = isActive;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new SupplierActivationChangedDomainEvent(Id, isActive, utcOccurredAt));
    }

    private static void ValidateStates(
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating)
    {
        if (!Enum.IsDefined(approvalStatus))
        {
            throw DomainGuard.Validation(nameof(approvalStatus), "Supplier approval status is not supported.");
        }

        if (!Enum.IsDefined(onboardingStatus))
        {
            throw DomainGuard.Validation(
                nameof(onboardingStatus),
                "Supplier onboarding status is not supported.");
        }

        if (!Enum.IsDefined(riskRating))
        {
            throw DomainGuard.Validation(nameof(riskRating), "Supplier risk rating is not supported.");
        }
    }

    private void CompleteMutation(ConcurrencyToken nextToken, DateTimeOffset occurredAt)
    {
        ConcurrencyToken = nextToken;
        LastModifiedAt = occurredAt;
    }
}
