using DecisionForge.Application.Platform;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DecisionForge.Api.IntegrationTests.Identity;

public sealed class IdentityApiFixture : PostgreSqlApiFixture
{
    public const string DemoPassword = "Local-Test-Password-2026!";

    public IdentityApiFixture()
        : base("decisionforge_phase13")
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        DecisionForgeIdentityDbContext database = scope.ServiceProvider
            .GetRequiredService<DecisionForgeIdentityDbContext>();
        await database.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityRoleSeeder>()
            .SeedAsync(CancellationToken.None);
        await CreateDemoSeeder(scope.ServiceProvider, "Demo").SeedAsync(CancellationToken.None);
    }

    public static DemoUserSeeder CreateDemoSeeder(
        IServiceProvider services,
        string environmentName)
    {
        return new DemoUserSeeder(
            services.GetRequiredService<UserManager<DecisionForgeUser>>(),
            services.GetRequiredService<IIdGenerator>(),
            Options.Create(new IdentitySeedOptions
            {
                Demo = new DemoIdentityOptions
                {
                    Enabled = true,
                    Password = DemoPassword,
                },
            }),
            new TestHostEnvironment(environmentName));
    }

    protected override PostgreSqlApiFactory CreateFactory()
    {
        return new IdentityApiFactory(ConnectionString);
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityApiTestGroup : ICollectionFixture<IdentityApiFixture>
{
    public const string Name = "Phase13IdentityApi";
}
