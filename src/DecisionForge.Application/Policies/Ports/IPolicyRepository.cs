using DecisionForge.Domain.Policies.Lifecycle;

namespace DecisionForge.Application.Policies.Ports;

public interface IPolicyRepository
{
    Task<Policy?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Policy policy, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
