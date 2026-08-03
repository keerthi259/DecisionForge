using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Ports;

public sealed record SupplierLookup(
    Guid Id,
    SupplierRegistrationNumber RegistrationNumber,
    string Name,
    SupplierApprovalStatus ApprovalStatus,
    SupplierOnboardingStatus OnboardingStatus,
    SupplierRiskRating RiskRating,
    bool IsActive);

public interface ISupplierQueries
{
    Task<SupplierLookup?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<SupplierLookup?> FindActiveByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SupplierLookup>> SearchActiveAsync(
        ReferenceDataPage page,
        CancellationToken cancellationToken);
}
