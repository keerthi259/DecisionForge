using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Ports;

public sealed record DepartmentLookup(
    Guid Id,
    DepartmentCode Code,
    string Name,
    Money AutoApprovalLimit);

public interface IDepartmentQueries
{
    Task<DepartmentLookup?> FindActiveByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DepartmentLookup>> SearchActiveAsync(
        ReferenceDataPage page,
        CancellationToken cancellationToken);
}
