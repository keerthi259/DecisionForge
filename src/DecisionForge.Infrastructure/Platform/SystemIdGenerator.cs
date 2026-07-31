using DecisionForge.Application.Platform;

namespace DecisionForge.Infrastructure.Platform;

public sealed class SystemIdGenerator(TimeProvider timeProvider) : IIdGenerator
{
    public Guid Create()
    {
        return Guid.CreateVersion7(timeProvider.GetUtcNow());
    }
}
