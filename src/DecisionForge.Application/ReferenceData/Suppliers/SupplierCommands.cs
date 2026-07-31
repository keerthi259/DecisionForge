using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Suppliers;

public sealed record CreateSupplierCommand(
    SupplierRegistrationNumber RegistrationNumber,
    string Name,
    SupplierApprovalStatus ApprovalStatus,
    SupplierOnboardingStatus OnboardingStatus,
    SupplierRiskRating RiskRating);

public sealed record UpdateSupplierCommand(
    Guid SupplierId,
    string Name,
    SupplierApprovalStatus ApprovalStatus,
    SupplierOnboardingStatus OnboardingStatus,
    SupplierRiskRating RiskRating,
    ConcurrencyToken ExpectedToken);

public sealed record SetSupplierActiveCommand(
    Guid SupplierId,
    bool IsActive,
    ConcurrencyToken ExpectedToken);
