using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Ports;

public interface ISupplierRepository
{
    Task<Supplier?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> RegistrationNumberExistsAsync(
        SupplierRegistrationNumber registrationNumber,
        CancellationToken cancellationToken);

    Task AddAsync(Supplier supplier, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
