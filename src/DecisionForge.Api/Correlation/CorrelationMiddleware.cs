using DecisionForge.Api.Configuration;
using DecisionForge.Application.Platform;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DecisionForge.Api.Correlation;

public sealed class CorrelationMiddleware(
    RequestDelegate next,
    IOptions<PlatformOptions> options,
    ICorrelationContextAccessor correlationContext,
    IIdGenerator idGenerator,
    ILogger<CorrelationMiddleware> logger)
{
    private const int _maximumCorrelationIdLength = 128;
    private readonly string _headerName = options.Value.CorrelationHeaderName;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context.Request.Headers[_headerName]);
        context.Response.Headers[_headerName] = correlationId;

        using IDisposable correlationScope = correlationContext.Push(correlationId);
        using IDisposable? loggingScope = logger.BeginScope(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["CorrelationId"] = correlationId,
            });

        await next(context);
    }

    private string ResolveCorrelationId(StringValues values)
    {
        if (values.Count == 1)
        {
            string? candidate = values[0];
            if (candidate is not null && IsValidCorrelationId(candidate))
            {
                return candidate;
            }
        }

        return idGenerator.Create().ToString("N");
    }

    private static bool IsValidCorrelationId(string value)
    {
        return value.Length is > 0 and <= _maximumCorrelationIdLength
            && value.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    }
}
