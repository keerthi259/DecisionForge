using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Policies.Ports;

public interface IPolicyQueries
{
    Task<bool> CodeExistsAsync(
        PolicyCode code,
        CancellationToken cancellationToken);
}
