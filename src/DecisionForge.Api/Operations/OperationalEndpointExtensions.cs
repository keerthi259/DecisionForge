using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace DecisionForge.Api.Operations;

public static class OperationalEndpointExtensions
{
    private static readonly string[] _liveTags = ["live"];

    public static IEndpointRouteBuilder MapOperationalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Overlaps(_liveTags),
            });
        endpoints.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = static _ => true,
            });
        endpoints.MapGet("/version", static () => Results.Ok(VersionResponse.Current));

        return endpoints;
    }
}
