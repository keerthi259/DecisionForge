namespace DecisionForge.Api.Foundation;

public static class ApiRouteExtensions
{
    public const string VersionOnePrefix = "/api/v1";

    public static RouteGroupBuilder MapApiVersionOne(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGroup(VersionOnePrefix);
    }
}
