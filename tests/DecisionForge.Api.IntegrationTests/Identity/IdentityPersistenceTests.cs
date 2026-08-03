using System.Security.Claims;
using DecisionForge.Application.Approvals.Ports;
using DecisionForge.Domain.Policies;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DecisionForge.Api.IntegrationTests.Identity;

[Collection(IdentityApiTestGroup.Name)]
public sealed class IdentityPersistenceTests(IdentityApiFixture fixture)
{
    [Fact]
    public void CookiePasswordAndLockoutSettingsAreSecure()
    {
        CookieAuthenticationOptions cookie = fixture.Factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Microsoft.AspNetCore.Identity.IdentityOptions identity = fixture.Factory.Services
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Identity.IdentityOptions>>().Value;

        Assert.Equal(DecisionForgeIdentityDefaults.AuthenticationCookieName, cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.True(cookie.Cookie.IsEssential);
        Assert.Equal("/", cookie.Cookie.Path);
        Assert.Equal(SameSiteMode.Strict, cookie.Cookie.SameSite);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(TimeSpan.FromHours(8), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
        Assert.True(identity.User.RequireUniqueEmail);
        Assert.True(identity.SignIn.RequireConfirmedEmail);
        Assert.True(identity.Lockout.AllowedForNewUsers);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), identity.Lockout.DefaultLockoutTimeSpan);
        Assert.Equal(12, identity.Password.RequiredLength);
    }

    [Fact]
    public async Task RoleAndDemoSeedersAreIdempotentAndPersistAllRoles()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        IdentityRoleSeeder roles = scope.ServiceProvider.GetRequiredService<IdentityRoleSeeder>();
        DemoUserSeeder users = IdentityApiFixture.CreateDemoSeeder(scope.ServiceProvider, "Demo");

        await roles.SeedAsync(CancellationToken.None);
        await roles.SeedAsync(CancellationToken.None);
        await users.SeedAsync(CancellationToken.None);
        await users.SeedAsync(CancellationToken.None);

        DecisionForgeIdentityDbContext database = scope.ServiceProvider
            .GetRequiredService<DecisionForgeIdentityDbContext>();
        Assert.Equal(DecisionForgeIdentityRoles.All.Count, await database.Roles.CountAsync());
        Assert.Equal(DemoUserSeeder.UserCount, await database.Users.CountAsync());
        string[] actualRoles = await database.Roles
            .Select(role => role.Name!)
            .OrderBy(name => name)
            .ToArrayAsync();
        Assert.Equal(DecisionForgeIdentityRoles.All.Order(StringComparer.Ordinal), actualRoles);
    }

    [Fact]
    public async Task ProductionEnvironmentNeverSeedsDemoUsersWhenSettingIsEnabled()
    {
        string connectionString = await fixture.CreateEmptyDatabaseAsync("phase13_production_seed");
        await using IdentityApiFactory factory = new(connectionString);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        DecisionForgeIdentityDbContext database = scope.ServiceProvider
            .GetRequiredService<DecisionForgeIdentityDbContext>();
        await database.Database.EnsureCreatedAsync();

        DemoUserSeeder seeder = IdentityApiFixture.CreateDemoSeeder(
            scope.ServiceProvider,
            "Production");
        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, await database.Users.CountAsync());
    }

    [Fact]
    public async Task IdentityApprovalAuthorizationMapsRolesAndExplicitOverridePermission()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<DecisionForgeUser> users = scope.ServiceProvider
            .GetRequiredService<UserManager<DecisionForgeUser>>();
        IApprovalAuthorization authorization = scope.ServiceProvider
            .GetRequiredService<IApprovalAuthorization>();
        DecisionForgeUser finance = Assert.IsType<DecisionForgeUser>(
            await users.FindByEmailAsync("finance@decisionforge.local"));
        DecisionForgeUser senior = Assert.IsType<DecisionForgeUser>(
            await users.FindByEmailAsync("senior@decisionforge.local"));
        DecisionForgeUser administrator = Assert.IsType<DecisionForgeUser>(
            await users.FindByEmailAsync("administrator@decisionforge.local"));

        Assert.Equal(
            [PolicyApproverRole.FinanceApprover],
            await authorization.GetApproverRolesAsync(finance.Id, CancellationToken.None));
        Assert.True(await authorization.CanOverrideDecisionAsync(
            senior.Id,
            CancellationToken.None));
        Assert.False(await authorization.CanOverrideDecisionAsync(
            administrator.Id,
            CancellationToken.None));
        IList<Claim> claims = await users.GetClaimsAsync(senior);
        Assert.Contains(
            claims,
            claim => claim.Type == DecisionForgeIdentityPermissions.ClaimType
                && claim.Value == DecisionForgeIdentityPermissions.OverrideDecision);
    }

    [Fact]
    public async Task IdentityPersistenceSurvivesIndependentDatabaseScopes()
    {
        Guid userId;
        await using (AsyncServiceScope first = fixture.Factory.Services.CreateAsyncScope())
        {
            DecisionForgeUser user = Assert.IsType<DecisionForgeUser>(
                await first.ServiceProvider.GetRequiredService<UserManager<DecisionForgeUser>>()
                    .FindByEmailAsync("requester@decisionforge.local"));
            userId = user.Id;
        }

        await using AsyncServiceScope second = fixture.Factory.Services.CreateAsyncScope();
        DecisionForgeUser persisted = Assert.IsType<DecisionForgeUser>(
            await second.ServiceProvider.GetRequiredService<UserManager<DecisionForgeUser>>()
                .FindByIdAsync(userId.ToString("D")));
        Assert.Equal("requester@decisionforge.local", persisted.Email);
        Assert.True(persisted.IsDemo);
    }

    [Fact]
    public async Task ExistingNonDemoIdentityCannotBeEscalatedByDemoSeeder()
    {
        string connectionString = await fixture.CreateEmptyDatabaseAsync("phase13_demo_collision");
        await using IdentityApiFactory factory = new(connectionString);
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        DecisionForgeIdentityDbContext database = scope.ServiceProvider
            .GetRequiredService<DecisionForgeIdentityDbContext>();
        await database.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityRoleSeeder>()
            .SeedAsync(CancellationToken.None);
        UserManager<DecisionForgeUser> manager = scope.ServiceProvider
            .GetRequiredService<UserManager<DecisionForgeUser>>();
        DecisionForgeUser existing = new()
        {
            Id = Guid.Parse("33333333-3333-4333-8333-333333333333"),
            UserName = "requester@decisionforge.local",
            Email = "requester@decisionforge.local",
            DisplayName = "Existing Account",
            EmailConfirmed = true,
            LockoutEnabled = true,
            IsDemo = false,
        };
        Assert.True((await manager.CreateAsync(existing, IdentityApiFixture.DemoPassword)).Succeeded);
        DemoUserSeeder seeder = IdentityApiFixture.CreateDemoSeeder(
            scope.ServiceProvider,
            "Demo");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(CancellationToken.None));

        Assert.Equal("identity.demo-user-collision", exception.Message);
        Assert.Empty(await manager.GetRolesAsync(existing));
    }

    [Fact]
    public async Task IdentityOperationsHonorCancellationAndHostedRoleSeedIsIdempotent()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        IdentityRoleSeeder seeder = scope.ServiceProvider.GetRequiredService<IdentityRoleSeeder>();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => seeder.SeedAsync(cancelled.Token));

        IdentitySeedHostedService hosted = new(
            fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IdentitySeedOptions { SeedRolesOnStartup = true }));
        await hosted.StartAsync(CancellationToken.None);
        await hosted.StopAsync(CancellationToken.None);
        DecisionForgeIdentityDbContext database = scope.ServiceProvider
            .GetRequiredService<DecisionForgeIdentityDbContext>();
        Assert.Equal(DecisionForgeIdentityRoles.All.Count, await database.Roles.CountAsync());
    }

    [Fact]
    public void EnabledDemoSeedRequiresStrongConfiguredPassword()
    {
        Assert.False(new IdentitySeedOptions
        {
            Demo = new DemoIdentityOptions { Enabled = true, Password = "weak" },
        }.IsValid());
        Assert.True(new IdentitySeedOptions
        {
            Demo = new DemoIdentityOptions
            {
                Enabled = true,
                Password = IdentityApiFixture.DemoPassword,
            },
        }.IsValid());
    }
}
