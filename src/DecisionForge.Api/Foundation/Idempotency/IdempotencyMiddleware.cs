using System.Buffers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Api.Foundation.Idempotency;

public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    private const int _maximumReplayBodyBytes = 1_048_576;
    private static readonly string[] _replayHeaders = ["ETag", "Location"];

    public async Task InvokeAsync(HttpContext context)
    {
        Endpoint? endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<RequireIdempotencyAttribute>() is null)
        {
            await next(context);
            return;
        }

        string endpointName = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
            ?? throw new InvalidOperationException(
                "Idempotent endpoints require a stable endpoint name.");
        string? actor = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(actor, out Guid actorId)
            || actorId == Guid.Empty)
        {
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication is required.",
                ApiErrorCodes.AuthenticationRequired,
                context.RequestAborted);
            return;
        }

        string scope = endpointName + ":" + actorId.ToString("N");
        if (!TryReadKey(context, out string key))
        {
            await ApiProblemWriter.WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "A valid Idempotency-Key header is required.",
                ApiErrorCodes.IdempotencyKeyRequired,
                context.RequestAborted);
            return;
        }

        string fingerprint = await CreateFingerprintAsync(context);
        ApiIdempotencyRequest request = new(scope, key, fingerprint);
        IApiIdempotencyStore store = context.RequestServices
            .GetRequiredService<IApiIdempotencyStore>();
        ApiIdempotencyBeginResult begin = await store.BeginAsync(
            request,
            context.RequestAborted);
        if (await HandleExistingAsync(context, begin))
        {
            return;
        }

        await ExecuteAndRecordAsync(context, store, request);
    }

    private async Task ExecuteAndRecordAsync(
        HttpContext context,
        IApiIdempotencyStore store,
        ApiIdempotencyRequest request)
    {
        Stream originalBody = context.Response.Body;
        await using MemoryStream capturedBody = new();
        context.Response.Body = capturedBody;
        try
        {
            await next(context);
            if (context.Response.StatusCode is >= 200 and < 300
                && capturedBody.Length <= _maximumReplayBodyBytes)
            {
                ApiIdempotencyResponse response = CaptureResponse(context, capturedBody);
                await store.CompleteAsync(request, response, context.RequestAborted);
            }
            else
            {
                await store.ReleaseAsync(request, CancellationToken.None);
            }

            capturedBody.Position = 0;
            await capturedBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        catch
        {
            await store.ReleaseAsync(request, CancellationToken.None);
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static ApiIdempotencyResponse CaptureResponse(
        HttpContext context,
        MemoryStream body)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in _replayHeaders)
        {
            if (context.Response.Headers.TryGetValue(name, out Microsoft.Extensions.Primitives.StringValues value))
            {
                headers[name] = value.ToString();
            }
        }

        return new ApiIdempotencyResponse(
            context.Response.StatusCode,
            context.Response.ContentType,
            body.ToArray(),
            headers);
    }

    private static async Task<bool> HandleExistingAsync(
        HttpContext context,
        ApiIdempotencyBeginResult begin)
    {
        switch (begin.Status)
        {
            case ApiIdempotencyBeginStatus.Acquired:
                return false;
            case ApiIdempotencyBeginStatus.Replay:
                await ReplayAsync(context, begin.Response
                    ?? throw new InvalidOperationException("A replay response is required."));
                return true;
            case ApiIdempotencyBeginStatus.Conflict:
                await ApiProblemWriter.WriteAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "The idempotency key was used with different input.",
                    ApiErrorCodes.IdempotencyConflict,
                    context.RequestAborted);
                return true;
            case ApiIdempotencyBeginStatus.InProgress:
                context.Response.Headers.RetryAfter = "1";
                await ApiProblemWriter.WriteAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "An operation with this idempotency key is still in progress.",
                    ApiErrorCodes.IdempotencyInProgress,
                    context.RequestAborted);
                return true;
            default:
                throw new InvalidOperationException("The idempotency store returned an invalid state.");
        }
    }

    private static async Task ReplayAsync(
        HttpContext context,
        ApiIdempotencyResponse response)
    {
        context.Response.StatusCode = response.StatusCode;
        context.Response.ContentType = response.ContentType;
        context.Response.Headers["Idempotency-Replayed"] = "true";
        foreach ((string name, string value) in response.Headers)
        {
            context.Response.Headers[name] = value;
        }

        await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
    }

    private static bool TryReadKey(HttpContext context, out string key)
    {
        Microsoft.Extensions.Primitives.StringValues values =
            context.Request.Headers["Idempotency-Key"];
        if (values.Count != 1)
        {
            key = string.Empty;
            return false;
        }

        try
        {
            key = IdempotencyKey.Parse(values[0]!).Value;
            return true;
        }
        catch (ArgumentException)
        {
            key = string.Empty;
            return false;
        }
        catch (DecisionForge.Domain.Common.DomainRuleException)
        {
            key = string.Empty;
            return false;
        }
    }

    private static async Task<string> CreateFingerprintAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, context.Request.Method);
        Append(hash, context.Request.Path.Value ?? string.Empty);
        Append(hash, context.Request.QueryString.Value ?? string.Empty);
        Append(hash, context.Request.ContentType ?? string.Empty);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            int read;
            while ((read = await context.Request.Body.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                context.RequestAborted)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            context.Request.Body.Position = 0;
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}
