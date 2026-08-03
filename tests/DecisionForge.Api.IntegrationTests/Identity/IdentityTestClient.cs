using System.Net.Http.Json;
using DecisionForge.Api.Identity;

namespace DecisionForge.Api.IntegrationTests.Identity;

internal static class IdentityTestClient
{
    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        AntiforgeryTokenResponse? token = await client.GetFromJsonAsync<AntiforgeryTokenResponse>(
            "/api/v1/auth/antiforgery",
            CancellationToken.None);
        Assert.NotNull(token);
        Assert.Equal(IdentityApiServiceCollectionExtensions.AntiforgeryHeaderName, token.HeaderName);
        return token.RequestToken;
    }

    public static HttpRequestMessage LoginRequest(
        string antiforgeryToken,
        string email,
        string password)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email, password)),
        };
        request.Headers.Add(
            IdentityApiServiceCollectionExtensions.AntiforgeryHeaderName,
            antiforgeryToken);
        return request;
    }
}
