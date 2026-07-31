using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Ports;

public interface IDepartmentRepository
{
    Task<Department?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        DepartmentCode code,
        CancellationToken cancellationToken);

    Task AddAsync(Department department, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
