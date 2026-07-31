using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.ReferenceData.Events;

public sealed record DepartmentCreatedDomainEvent(
    Guid DepartmentId,
    DepartmentCode Code,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DepartmentDetailsChangedDomainEvent(
    Guid DepartmentId,
    Money AutoApprovalLimit,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DepartmentActivationChangedDomainEvent(
    Guid DepartmentId,
    bool IsActive,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SupplierCreatedDomainEvent(
    Guid SupplierId,
    SupplierRegistrationNumber RegistrationNumber,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SupplierDetailsChangedDomainEvent(
    Guid SupplierId,
    SupplierApprovalStatus ApprovalStatus,
    SupplierOnboardingStatus OnboardingStatus,
    SupplierRiskRating RiskRating,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record SupplierActivationChangedDomainEvent(
    Guid SupplierId,
    bool IsActive,
    DateTimeOffset OccurredAt) : IDomainEvent;
