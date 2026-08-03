using System.Net;
using System.Net.Http.Json;
using DecisionForge.Api.Foundation;
using DecisionForge.Api.Identity;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionForge.Api.IntegrationTests.Identity;

[Collection(IdentityApiTestGroup.Name)]
public sealed class AuthenticationEndpointTests(IdentityApiFixture fixture)
{
    [Fact]
    public async Task LoginWithoutAntiforgeryTokenIsRejected()
    {
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("requester@decisionforge.local", IdentityApiFixture.DemoPassword),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoginMeAndLogoutUseSecureCookieAndRotatedAntiforgeryToken()
    {
        using HttpClient client = fixture.CreateClient();
        string loginToken = await IdentityTestClient.GetAntiforgeryTokenAsync(client);
        using HttpRequestMessage loginRequest = IdentityTestClient.LoginRequest(
            loginToken,
            "requester@decisionforge.local",
            IdentityApiFixture.DemoPassword);
        using HttpResponseMessage login = await client.SendAsync(loginRequest);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        string cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value =>
            value.StartsWith(
                $"{DecisionForgeIdentityDefaults.AuthenticationCookieName}=",
                StringComparison.Ordinal));
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        CurrentUserResponse? me = await client.GetFromJsonAsync<CurrentUserResponse>(
            "/api/v1/auth/me",
            CancellationToken.None);
        Assert.NotNull(me);
        Assert.Equal("requester@decisionforge.local", me.Email);
        Assert.Equal([DecisionForgeIdentityRoles.Requester], me.Roles);
        Assert.Empty(me.Permissions);

        using HttpResponseMessage missingToken = await client.PostAsync(
            "/api/v1/auth/logout",
            content: null,
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/auth/me", CancellationToken.None)).StatusCode);

        string logoutToken = await IdentityTestClient.GetAntiforgeryTokenAsync(client);
        using HttpRequestMessage logoutRequest = new(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Add(
            IdentityApiServiceCollectionExtensions.AntiforgeryHeaderName,
            logoutToken);
        using HttpResponseMessage logout = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/auth/me", CancellationToken.None)).StatusCode);
    }

    [Fact]
    public async Task AnonymousMeAndMalformedOrInvalidCredentialsAreControlled()
    {
        using HttpClient client = fixture.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/auth/me", CancellationToken.None)).StatusCode);
        string token = await IdentityTestClient.GetAntiforgeryTokenAsync(client);

        using HttpRequestMessage malformed = IdentityTestClient.LoginRequest(token, "bad email", string.Empty);
        using HttpResponseMessage malformedResponse = await client.SendAsync(malformed);
        ApiProblemDetails? validation = await malformedResponse.Content
            .ReadFromJsonAsync<ApiProblemDetails>(CancellationToken.None);
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationField, validation?.ErrorCode);

        using HttpRequestMessage invalid = IdentityTestClient.LoginRequest(
            token,
            "missing@decisionforge.local",
            "Definitely-Wrong-2026!");
        using HttpResponseMessage invalidResponse = await client.SendAsync(invalid);
        string invalidBody = await invalidResponse.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        Assert.Contains("authentication.invalid-credentials", invalidBody, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", invalidBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", invalidBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedInvalidPasswordLocksAccountAndCorrectPasswordDoesNotBypassLockout()
    {
        await ResetLockoutAsync("finance@decisionforge.local");
        using HttpClient client = fixture.CreateClient();
        string token = await IdentityTestClient.GetAntiforgeryTokenAsync(client);
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            using HttpRequestMessage invalid = IdentityTestClient.LoginRequest(
                token,
                "finance@decisionforge.local",
                "Definitely-Wrong-2026!");
            using HttpResponseMessage response = await client.SendAsync(invalid);
            Assert.Equal(
                attempt == 5 ? HttpStatusCode.Locked : HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        using HttpRequestMessage correct = IdentityTestClient.LoginRequest(
            token,
            "finance@decisionforge.local",
            IdentityApiFixture.DemoPassword);
        using HttpResponseMessage locked = await client.SendAsync(correct);
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
        await ResetLockoutAsync("finance@decisionforge.local");
    }

    [Fact]
    public async Task LoginEndpointAppliesIpPartitionedRateLimit()
    {
        await using IdentityApiFactory factory = new(fixture.ConnectionString, loginPermitLimit: 2);
        using HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost", UriKind.Absolute),
            HandleCookies = true,
        });
        string token = await IdentityTestClient.GetAntiforgeryTokenAsync(client);
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            using HttpRequestMessage invalid = IdentityTestClient.LoginRequest(
                token,
                "absent@decisionforge.local",
                "Definitely-Wrong-2026!");
            using HttpResponseMessage response = await client.SendAsync(invalid);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using HttpRequestMessage limited = IdentityTestClient.LoginRequest(
            token,
            "absent@decisionforge.local",
            "Definitely-Wrong-2026!");
        using HttpResponseMessage limitedResponse = await client.SendAsync(limited);
        string body = await limitedResponse.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.True(limitedResponse.Headers.Contains("Retry-After"));
        Assert.Contains(ApiErrorCodes.RateLimit, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AntiforgeryTokenFromAnotherCookieSessionIsRejected()
    {
        using HttpClient first = fixture.CreateClient();
        using HttpClient second = fixture.CreateClient();
        string firstToken = await IdentityTestClient.GetAntiforgeryTokenAsync(first);
        _ = await IdentityTestClient.GetAntiforgeryTokenAsync(second);

        using HttpRequestMessage mismatched = IdentityTestClient.LoginRequest(
            firstToken,
            "requester@decisionforge.local",
            IdentityApiFixture.DemoPassword);
        using HttpResponseMessage response = await second.SendAsync(mismatched);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("authentication.antiforgery-invalid", body, StringComparison.Ordinal);
    }

    private async Task ResetLockoutAsync(string email)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<DecisionForgeUser> manager = scope.ServiceProvider
            .GetRequiredService<UserManager<DecisionForgeUser>>();
        DecisionForgeUser user = Assert.IsType<DecisionForgeUser>(await manager.FindByEmailAsync(email));
        Assert.True((await manager.SetLockoutEndDateAsync(user, null)).Succeeded);
        Assert.True((await manager.ResetAccessFailedCountAsync(user)).Succeeded);
    }
}
