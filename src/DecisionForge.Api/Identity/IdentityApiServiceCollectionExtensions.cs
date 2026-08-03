using System.Threading.RateLimiting;
using DecisionForge.Api.Authorization;
using DecisionForge.Application.Platform;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DecisionForge.Api.Identity;

public static class IdentityApiServiceCollectionExtensions
{
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    public const string LoginRateLimitPolicyName = "identity-login";

    public static IServiceCollection AddDecisionForgeIdentityApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.ConfigureApplicationCookie(ConfigureCookie);
        services.AddAntiforgery(ConfigureAntiforgery);
        services.AddAuthorization(ConfigureAuthorization);
        services.AddSingleton<IAuthorizationHandler, PurchaseRequestAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, ApprovalStageAuthorizationHandler>();
        services.AddOptions<IdentityApiOptions>()
            .Bind(configuration.GetSection(IdentityApiOptions.SectionName))
            .Validate(options => options.IsValid(), "Identity API configuration is invalid.")
            .ValidateOnStart();
        services.AddRateLimiter(ConfigureRateLimiting);
        return services;
    }

    private static void ConfigureAntiforgery(AntiforgeryOptions options)
    {
        options.HeaderName = AntiforgeryHeaderName;
        options.SuppressReadingTokenFromFormBody = true;
        options.Cookie.Name = "__Host-DecisionForge-Antiforgery";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = DecisionForgeIdentityDefaults.AuthenticationCookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    private static void ConfigureAuthorization(AuthorizationOptions options)
    {
        options.AddPolicy(
            AuthorizationPolicyNames.CanCreateRequest,
            policy => policy.RequireRole(DecisionForgeIdentityRoles.Requester));
        AddPurchaseRequestPolicy(
            options,
            AuthorizationPolicyNames.CanReadPurchaseRequest,
            PurchaseRequestAuthorizationOperation.Read);
        AddPurchaseRequestPolicy(
            options,
            AuthorizationPolicyNames.CanEditPurchaseRequest,
            PurchaseRequestAuthorizationOperation.Edit);
        AddPurchaseRequestPolicy(
            options,
            AuthorizationPolicyNames.CanSubmitPurchaseRequest,
            PurchaseRequestAuthorizationOperation.Submit);
        options.AddPolicy(
            AuthorizationPolicyNames.CanActOnApprovalStage,
            policy => policy.AddRequirements(new ActOnApprovalStageRequirement()));
        options.AddPolicy(
            AuthorizationPolicyNames.CanAuthorPolicy,
            policy => policy.RequireRole(DecisionForgeIdentityRoles.PolicyAuthor));
        options.AddPolicy(
            AuthorizationPolicyNames.CanPublishPolicy,
            policy => policy.RequireRole(DecisionForgeIdentityRoles.PolicyPublisher));
        options.AddPolicy(
            AuthorizationPolicyNames.CanReadAudit,
            policy => policy.RequireRole(DecisionForgeIdentityRoles.Auditor));
        options.AddPolicy(
            AuthorizationPolicyNames.CanManageReferenceData,
            policy => policy.RequireRole(DecisionForgeIdentityRoles.Administrator));
        options.AddPolicy(
            AuthorizationPolicyNames.CanOverrideDecision,
            policy => policy.RequireClaim(
                DecisionForgeIdentityPermissions.ClaimType,
                DecisionForgeIdentityPermissions.OverrideDecision));
    }

    private static void AddPurchaseRequestPolicy(
        AuthorizationOptions options,
        string name,
        PurchaseRequestAuthorizationOperation operation)
    {
        options.AddPolicy(
            name,
            policy => policy.AddRequirements(new PurchaseRequestAuthorizationRequirement(operation)));
    }

    private static void ConfigureRateLimiting(RateLimiterOptions options)
    {
        options.AddPolicy(
            LoginRateLimitPolicyName,
            context =>
            {
                IdentityApiOptions settings = context.RequestServices
                    .GetRequiredService<IOptions<IdentityApiOptions>>().Value;
                string partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = settings.PermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    });
            });
    }
}
