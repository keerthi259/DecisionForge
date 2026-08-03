using System.Security.Claims;
using System.Threading.RateLimiting;
using DecisionForge.Api.Exports;
using DecisionForge.Api.Foundation;
using DecisionForge.Api.Foundation.Idempotency;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionForge.Api.IntegrationTests.Foundation;

internal sealed class ApiFoundationTestApp : IAsyncDisposable
{
    private static readonly Guid _currentToken = Guid.Parse("42c45dac-36a8-44f5-a279-03420150393c");
    private readonly WebApplication _application;

    private ApiFoundationTestApp(WebApplication application)
    {
        _application = application;
        Client = application.GetTestClient();
        Client.BaseAddress = new Uri("https://localhost", UriKind.Absolute);
    }

    public HttpClient Client { get; }

    public static async Task<ApiFoundationTestApp> StartAsync(
        int maximumBodyBytes = 1024,
        bool authenticated = true)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.FullName,
            EnvironmentName = "Testing",
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DecisionForge:Api:MaximumRequestBodyBytes"] =
                maximumBodyBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        builder.Services.AddDecisionForgeApiFoundation(builder.Configuration);
        builder.Services.AddSingleton<IApiIdempotencyStore, TestApiIdempotencyStore>();
        builder.Services.AddSingleton<TestInvocationCounter>();
        builder.Services.AddRateLimiter(options => options.AddPolicy(
            "test-endpoint",
            _ => RateLimitPartition.GetFixedWindowLimiter(
                "test",
                static _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 1,
                    QueueLimit = 0,
                    Window = TimeSpan.FromMinutes(1),
                })));

        WebApplication app = builder.Build();
        app.UseExceptionHandler();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestBodyLimitMiddleware>();
        app.UseStatusCodePages(ApiStatusCodePages.WriteAsync);
        app.UseCors(ApiFoundationServiceCollectionExtensions.CorsPolicyName);
        app.UseRateLimiter();
        if (authenticated)
        {
            app.Use(static (context, next) =>
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "c932c11a-2441-459a-b80b-6b60230d35e4")],
                    authenticationType: "Phase14Test"));
                return next(context);
            });
        }

        app.UseMiddleware<IdempotencyMiddleware>();
        MapEndpoints(app.MapApiVersionOne());
        await app.StartAsync();
        return new ApiFoundationTestApp(app);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }

    private static void MapEndpoints(RouteGroupBuilder api)
    {
        ApiListQueryDefinition listDefinition = new(
            ["createdAt", "name"],
            ["status", "search"],
            "createdAt",
            ApiSortDirection.Descending);
        api.MapGet("/test/list", (HttpRequest request) =>
            Results.Ok(ApiListQueryParser.Parse(request.Query, listDefinition)));
        api.MapGet("/test/business-error", static () =>
            Throw(new DomainRuleException(
                DomainErrorCodes.Validation,
                "The supplied business value is invalid.",
                "amount")));
        api.MapGet("/test/internal-error", static () =>
            Throw(new InvalidOperationException("secret-database-diagnostic")));
        api.MapPut("/test/etag", static (HttpContext context) =>
        {
            ConcurrencyToken supplied = EntityTagSupport.ParseRequired(context.Request);
            if (supplied.Value != _currentToken)
            {
                throw new DomainRuleException(
                    DomainErrorCodes.ConcurrencyConflict,
                    "The supplied concurrency token is stale.");
            }

            EntityTagSupport.Set(context.Response, supplied);
            return Results.NoContent();
        });
        api.MapPost("/test/idempotency", InvokeIdempotentAsync)
            .WithName("Phase14IdempotencyProbe")
            .WithMetadata(new RequireIdempotencyAttribute());
        api.MapPost("/test/body", static async (HttpRequest request, CancellationToken cancellationToken) =>
        {
            using MemoryStream body = new();
            await request.Body.CopyToAsync(body, cancellationToken);
            return Results.Ok(new { length = body.Length });
        });
        api.MapGet("/test/rate", static () => Results.NoContent())
            .RequireRateLimiting("test-endpoint");
    }

    private static IResult Throw(Exception exception)
    {
        throw exception;
    }

    private static async Task<IResult> InvokeIdempotentAsync(
        HttpRequest request,
        TestInvocationCounter counter,
        CancellationToken cancellationToken)
    {
        using StreamReader reader = new(request.Body, leaveOpen: true);
        string body = await reader.ReadToEndAsync(cancellationToken);
        int invocation = Interlocked.Increment(ref counter.Value);
        return Results.Ok(new { invocation, body });
    }
}

internal sealed class TestInvocationCounter
{
    public int Value;
}

internal sealed class TestApiIdempotencyStore : IApiIdempotencyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public Task<ApiIdempotencyBeginResult> BeginAsync(
        ApiIdempotencyRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            string identity = Identity(request);
            if (!_entries.TryGetValue(identity, out Entry? entry))
            {
                _entries.Add(identity, new Entry(request.Fingerprint));
                return Task.FromResult(new ApiIdempotencyBeginResult(
                    ApiIdempotencyBeginStatus.Acquired));
            }

            if (!string.Equals(entry.Fingerprint, request.Fingerprint, StringComparison.Ordinal))
            {
                return Task.FromResult(new ApiIdempotencyBeginResult(
                    ApiIdempotencyBeginStatus.Conflict));
            }

            return Task.FromResult(entry.Response is null
                ? new ApiIdempotencyBeginResult(ApiIdempotencyBeginStatus.InProgress)
                : new ApiIdempotencyBeginResult(
                    ApiIdempotencyBeginStatus.Replay,
                    entry.Response));
        }
    }

    public Task CompleteAsync(
        ApiIdempotencyRequest request,
        ApiIdempotencyResponse response,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Entry entry = _entries[Identity(request)];
            if (!string.Equals(entry.Fingerprint, request.Fingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The test store fingerprint changed.");
            }

            entry.Response = response;
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(
        ApiIdempotencyRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            string identity = Identity(request);
            if (_entries.TryGetValue(identity, out Entry? entry) && entry.Response is null)
            {
                _entries.Remove(identity);
            }
        }

        return Task.CompletedTask;
    }

    private static string Identity(ApiIdempotencyRequest request)
    {
        return request.Scope + "\0" + request.Key;
    }

    private sealed class Entry(string fingerprint)
    {
        public string Fingerprint { get; } = fingerprint;

        public ApiIdempotencyResponse? Response { get; set; }
    }
}
