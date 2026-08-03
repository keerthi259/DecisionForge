using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionForge.Api.IntegrationTests.Foundation;

public sealed class ApiContractIntegrationTests
{
    [Fact]
    public async Task AllApiRoutesUseVersionOnePrefix()
    {
        await using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/openapi/v1.json",
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        EndpointDataSource dataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        string[] allowedOperationalRoutes = ["/health/live", "/health/ready", "/version"];
        string[] routes = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/'))
            .Where(route => !route.StartsWith("/_", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.All(routes, route => Assert.True(
            route.StartsWith("/api/v1", StringComparison.Ordinal)
            || allowedOperationalRoutes.Contains(route, StringComparer.Ordinal),
            $"Route '{route}' is neither versioned nor an approved operational route."));
        Assert.DoesNotContain("/auth/me", routes);
    }

    [Fact]
    public async Task OpenApiDocumentsCookieAuthenticationExamplesAndProblemSchema()
    {
        await using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/openapi/v1.json",
            CancellationToken.None);
        string document = await response.Content.ReadAsStringAsync(CancellationToken.None);

        response.EnsureSuccessStatusCode();
        Assert.Contains("\"openapi\": \"3.1.1\"", document, StringComparison.Ordinal);
        Assert.Contains("\"cookieAuth\"", document, StringComparison.Ordinal);
        Assert.Contains("__Host-DecisionForge-Auth", document, StringComparison.Ordinal);
        Assert.Contains("requester@decisionforge.local", document, StringComparison.Ordinal);
        Assert.Contains("validation.field", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/auth/login", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownApiRouteReturnsProblemDetailsAndSecurityHeaders()
    {
        await using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/not-a-route",
            CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("resource.not-found", body, StringComparison.Ordinal);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }
}
