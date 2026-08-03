using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DecisionForge.Infrastructure.Identity;

public sealed class IdentitySeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<IdentitySeedOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IdentitySeedOptions settings = options.Value;
        if (!settings.SeedRolesOnStartup && !settings.Demo.Enabled)
        {
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IdentityRoleSeeder roleSeeder = scope.ServiceProvider.GetRequiredService<IdentityRoleSeeder>();
        await roleSeeder.SeedAsync(cancellationToken);

        DemoUserSeeder demoSeeder = scope.ServiceProvider.GetRequiredService<DemoUserSeeder>();
        await demoSeeder.SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
