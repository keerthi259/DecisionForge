using DecisionForge.Application.Platform;
using DecisionForge.Infrastructure.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DecisionForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDecisionForgeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IIdGenerator, SystemIdGenerator>();
        services.TryAddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();

        return services;
    }
}
