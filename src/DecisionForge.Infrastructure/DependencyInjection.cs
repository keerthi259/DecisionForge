using DecisionForge.Application.Platform;
using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Application.Reliability.Outbox;
using DecisionForge.Infrastructure.Identity;
using DecisionForge.Infrastructure.Platform;
using DecisionForge.Infrastructure.Reliability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DecisionForge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDecisionForgeInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IIdGenerator, SystemIdGenerator>();
        services.TryAddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
        if (configuration is not null)
        {
            services.AddDecisionForgeIdentity(configuration);
            services.AddOptions<ReliabilityOptions>()
                .Bind(configuration.GetSection(ReliabilityOptions.SectionName))
                .Validate(options => options.IsValid(), "Reliability configuration is invalid.")
                .ValidateOnStart();
            services.AddSingleton(provider =>
            {
                ReliabilityOptions options = provider.GetRequiredService<
                    IOptions<ReliabilityOptions>>().Value;
                return new OutboxDispatcherOptions
                {
                    BatchSize = options.BatchSize,
                    PollInterval = TimeSpan.FromSeconds(options.PollIntervalSeconds),
                    CompletedRetention = TimeSpan.FromDays(options.CompletedRetentionDays),
                };
            });
            services.AddSingleton<PostgresReliabilityStore>();
            services.AddSingleton<IOutboxStore>(provider =>
                provider.GetRequiredService<PostgresReliabilityStore>());
            services.AddSingleton<INotificationStore>(provider =>
                provider.GetRequiredService<PostgresReliabilityStore>());
            services.AddSingleton<IOutboxMessageHandler, NotificationOutboxHandler>();
            services.AddSingleton<OutboxDispatcher>();
            services.AddHttpClient<INotificationSender, MailpitNotificationSender>(
                (provider, client) =>
                {
                    ReliabilityOptions options = provider.GetRequiredService<
                        IOptions<ReliabilityOptions>>().Value;
                    client.BaseAddress = new Uri(options.MailpitBaseAddress, UriKind.Absolute);
                });
            services.AddHostedService<OutboxWorker>();
        }

        return services;
    }
}
