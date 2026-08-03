using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DecisionForge.Api.Foundation;

namespace DecisionForge.Api.IntegrationTests.Foundation;

public sealed class ApiFoundationIntegrationTests
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task FieldAndBusinessValidationHaveDistinctSafeContracts()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();

        using HttpResponseMessage field = await app.Client.GetAsync(
            "/api/v1/test/list?sort=secretField",
            CancellationToken.None);
        ApiProblemDetails? fieldProblem = await field.Content.ReadFromJsonAsync<ApiProblemDetails>();
        using HttpResponseMessage business = await app.Client.GetAsync(
            "/api/v1/test/business-error",
            CancellationToken.None);
        ApiProblemDetails? businessProblem = await business.Content
            .ReadFromJsonAsync<ApiProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, field.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationField, fieldProblem?.ErrorCode);
        Assert.Equal("query.sort.unsupported", Assert.Single(fieldProblem!.Errors!).Code);
        Assert.Equal((HttpStatusCode)422, business.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationBusiness, businessProblem?.ErrorCode);
        Assert.Equal("domain.validation", Assert.Single(businessProblem!.Errors!).Code);
        Assert.False(string.IsNullOrWhiteSpace(fieldProblem.TraceId));
    }

    [Fact]
    public async Task UnexpectedExceptionReturnsSafeProblemWithoutDiagnostic()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();

        using HttpResponseMessage response = await app.Client.GetAsync(
            "/api/v1/test/internal-error",
            CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);
        ApiProblemDetails? problem = JsonSerializer.Deserialize<ApiProblemDetails>(
            body,
            _jsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ApiErrorCodes.InternalError, problem?.ErrorCode);
        Assert.DoesNotContain("secret-database-diagnostic", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaginationIsBoundedAndSortsAndFiltersAreAllowListed()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();

        using HttpResponseMessage valid = await app.Client.GetAsync(
            "/api/v1/test/list?offset=5&pageSize=100&sort=-name&status=Draft",
            CancellationToken.None);
        using HttpResponseMessage oversized = await app.Client.GetAsync(
            "/api/v1/test/list?pageSize=101",
            CancellationToken.None);
        using HttpResponseMessage unsupported = await app.Client.GetAsync(
            "/api/v1/test/list?ownerId=hidden",
            CancellationToken.None);

        valid.EnsureSuccessStatusCode();
        string validBody = await valid.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("\"pageSize\":100", validBody, StringComparison.Ordinal);
        Assert.Contains("\"direction\":2", validBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Contains(
            "query.filter.unsupported",
            await unsupported.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task StrongEtagsRequireOneValidIfMatchAndMapStaleConflict()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();
        using HttpRequestMessage validRequest = new(HttpMethod.Put, "/api/v1/test/etag");
        validRequest.Headers.TryAddWithoutValidation(
            "If-Match",
            "\"42c45dac36a844f5a27903420150393c\"");
        using HttpRequestMessage staleRequest = new(HttpMethod.Put, "/api/v1/test/etag");
        staleRequest.Headers.TryAddWithoutValidation(
            "If-Match",
            "\"67f56b5b0e9848969ba0746896c5ba53\"");

        using HttpResponseMessage valid = await app.Client.SendAsync(validRequest);
        using HttpResponseMessage missing = await app.Client.PutAsync(
            "/api/v1/test/etag",
            content: null,
            CancellationToken.None);
        using HttpResponseMessage malformed = await PutWithIfMatchAsync(app.Client, "W/\"weak\"");
        using HttpResponseMessage stale = await app.Client.SendAsync(staleRequest);

        Assert.Equal(HttpStatusCode.NoContent, valid.StatusCode);
        Assert.Equal("\"42c45dac36a844f5a27903420150393c\"", valid.Headers.ETag?.Tag);
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Contains(
            ApiErrorCodes.ConcurrencyConflict,
            await stale.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdempotencyReplaysOriginalAndRejectsChangedFingerprint()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();

        using HttpResponseMessage first = await PostIdempotentAsync(app.Client, "operation-1", "same");
        string firstBody = await first.Content.ReadAsStringAsync(CancellationToken.None);
        using HttpResponseMessage replay = await PostIdempotentAsync(app.Client, "operation-1", "same");
        string replayBody = await replay.Content.ReadAsStringAsync(CancellationToken.None);
        using HttpResponseMessage conflict = await PostIdempotentAsync(app.Client, "operation-1", "changed");
        using HttpResponseMessage missing = await app.Client.PostAsync(
            "/api/v1/test/idempotency",
            new StringContent("same", Encoding.UTF8, "text/plain"),
            CancellationToken.None);

        Assert.Equal(firstBody, replayBody);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Assert.Contains("\"invocation\":1", firstBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains(
            ApiErrorCodes.IdempotencyConflict,
            await conflict.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task IdempotencyNeverReplaysAcrossAnonymousScope()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync(
            authenticated: false);

        using HttpResponseMessage response = await PostIdempotentAsync(
            app.Client,
            "anonymous-operation",
            "input");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            ApiErrorCodes.AuthenticationRequired,
            await response.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task BodyLimitSecurityHeadersAndRestrictiveCorsAreApplied()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();
        using ByteArrayContent excessiveBody = new(new byte[1025]);
        using HttpResponseMessage excessive = await app.Client.PostAsync(
            "/api/v1/test/body",
            excessiveBody,
            CancellationToken.None);
        using HttpRequestMessage foreignRequest = new(HttpMethod.Get, "/api/v1/test/list");
        foreignRequest.Headers.TryAddWithoutValidation("Origin", "https://attacker.example");
        using HttpResponseMessage foreign = await app.Client.SendAsync(foreignRequest);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, excessive.StatusCode);
        Assert.Contains(
            ApiErrorCodes.BodyTooLarge,
            await excessive.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(foreign.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(foreign.Headers.GetValues("X-Frame-Options")));
        Assert.False(foreign.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task EndpointRateLimitReturnsProblemAndRetryInformation()
    {
        await using ApiFoundationTestApp app = await ApiFoundationTestApp.StartAsync();

        using HttpResponseMessage first = await app.Client.GetAsync(
            "/api/v1/test/rate",
            CancellationToken.None);
        using HttpResponseMessage second = await app.Client.GetAsync(
            "/api/v1/test/rate",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Contains(
            ApiErrorCodes.RateLimit,
            await second.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> PutWithIfMatchAsync(
        HttpClient client,
        string etag)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, "/api/v1/test/etag");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostIdempotentAsync(
        HttpClient client,
        string key,
        string body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/test/idempotency")
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
}
