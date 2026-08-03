using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace DecisionForge.Api.Foundation;

public static class ApiFoundationServiceCollectionExtensions
{
    public const string CorsPolicyName = "decisionforge-same-origin";

    public static IServiceCollection AddDecisionForgeApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(ApiFoundationOptions.SectionName);
        ApiFoundationOptions settings = section.Get<ApiFoundationOptions>() ?? new ApiFoundationOptions();
        if (!settings.IsValid())
        {
            throw new InvalidOperationException("DecisionForge API configuration is invalid.");
        }

        services.AddOptions<ApiFoundationOptions>()
            .Bind(section)
            .Validate(options => options.IsValid(), "DecisionForge API configuration is invalid.")
            .ValidateOnStart();
        services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = settings.MaximumRequestBodyBytes;
        });
        services.Configure<IISServerOptions>(options =>
            options.MaxRequestBodySize = settings.MaximumRequestBodyBytes);
        services.Configure<JsonOptions>(options =>
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();
        services.AddCors(options => options.AddPolicy(
            CorsPolicyName,
            policy => ConfigureCors(policy, settings.AllowedCorsOrigins)));
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ApiOpenApiDocumentTransformer>();
            options.AddOperationTransformer<ApiOpenApiOperationTransformer>();
            options.AddSchemaTransformer<ApiOpenApiSchemaTransformer>();
        });
        services.PostConfigure<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>(options =>
        {
            options.OnRejected = WriteRateLimitProblemAsync;
        });
        return services;
    }

    private static void ConfigureCors(CorsPolicyBuilder policy, string[] allowedOrigins)
    {
        if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(static _ => false);
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders(
                "Content-Type",
                "Idempotency-Key",
                "If-Match",
                Identity.IdentityApiServiceCollectionExtensions.AntiforgeryHeaderName)
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    }

    private static async ValueTask WriteRateLimitProblemAsync(
        Microsoft.AspNetCore.RateLimiting.OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(
            System.Threading.RateLimiting.MetadataName.RetryAfter,
            out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(
                retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await ApiProblemWriter.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "The request rate limit was exceeded.",
            ApiErrorCodes.RateLimit,
            cancellationToken);
    }
}
