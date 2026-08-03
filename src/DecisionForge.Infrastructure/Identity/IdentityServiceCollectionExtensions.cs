using DecisionForge.Application.Approvals.Ports;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionForge.Infrastructure.Identity;

internal static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddDecisionForgeIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("decisionforge")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:decisionforge is required for Identity persistence.");

        services.AddDbContext<DecisionForgeIdentityDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgres => postgres.MigrationsHistoryTable(
                    "__ef_migrations_history",
                    DecisionForgeIdentityDbContext.SchemaName)));
        services
            .AddIdentity<DecisionForgeUser, IdentityRole<Guid>>(ConfigureIdentity)
            .AddEntityFrameworkStores<DecisionForgeIdentityDbContext>()
            .AddDefaultTokenProviders();
        services.AddOptions<IdentitySeedOptions>()
            .Bind(configuration.GetSection(IdentitySeedOptions.SectionName))
            .Validate(options => options.IsValid(), "Identity seed configuration is invalid.")
            .ValidateOnStart();
        services.AddScoped<IdentityRoleSeeder>();
        services.AddScoped<DemoUserSeeder>();
        services.AddScoped<IApprovalAuthorization, IdentityApprovalAuthorization>();
        services.AddHostedService<IdentitySeedHostedService>();
        return services;
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }

}
