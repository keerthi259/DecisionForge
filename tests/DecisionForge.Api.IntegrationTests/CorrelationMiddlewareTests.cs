using DecisionForge.Api.Configuration;
using DecisionForge.Api.Correlation;
using DecisionForge.Application.Platform;
using DecisionForge.Infrastructure.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DecisionForge.Api.IntegrationTests;

public sealed class CorrelationMiddlewareTests
{
    private const string _headerName = "X-Correlation-ID";

    [Fact]
    public async Task ValidIncomingCorrelationIdIsSharedByResponseLogAndApplicationContext()
    {
        const string expected = "phase-3-smoke";
        CorrelationContextAccessor accessor = new();
        ScopeCapturingLogger logger = new();
        string? observedApplicationCorrelation = null;
        DefaultHttpContext context = new();
        context.Request.Headers[_headerName] = expected;
        CorrelationMiddleware middleware = CreateMiddleware(
            _ =>
            {
                observedApplicationCorrelation = accessor.CorrelationId;
                return Task.CompletedTask;
            },
            accessor,
            logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(expected, context.Response.Headers[_headerName]);
        Assert.Equal(expected, observedApplicationCorrelation);
        Assert.Equal(expected, logger.CorrelationId);
        Assert.Null(accessor.CorrelationId);
    }

    [Fact]
    public async Task MalformedIncomingCorrelationIdIsReplaced()
    {
        CorrelationContextAccessor accessor = new();
        ScopeCapturingLogger logger = new();
        DefaultHttpContext context = new();
        context.Request.Headers[_headerName] = "contains a log-breaking space";
        CorrelationMiddleware middleware = CreateMiddleware(
            static _ => Task.CompletedTask,
            accessor,
            logger);

        await middleware.InvokeAsync(context);

        string actual = Assert.Single(context.Response.Headers[_headerName])!;
        Assert.NotEqual("contains a log-breaking space", actual);
        Assert.Equal(32, actual.Length);
        Assert.All(actual, character => Assert.True(char.IsAsciiHexDigit(character)));
    }

    private static CorrelationMiddleware CreateMiddleware(
        RequestDelegate next,
        ICorrelationContextAccessor accessor,
        ILogger<CorrelationMiddleware> logger)
    {
        PlatformOptions platformOptions = new()
        {
            ApplicationName = "DecisionForge.Api",
            CorrelationHeaderName = _headerName,
        };

        return new CorrelationMiddleware(
            next,
            Options.Create(platformOptions),
            accessor,
            new FixedIdGenerator(),
            logger);
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        public Guid Create()
        {
            return Guid.ParseExact("0198a1fd15ec7f2ea1d5f0ab2331379c", "N");
        }
    }

    private sealed class ScopeCapturingLogger : ILogger<CorrelationMiddleware>
    {
        public string? CorrelationId { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object>> values)
            {
                CorrelationId = values.Single(pair => pair.Key == "CorrelationId").Value.ToString();
            }

            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
