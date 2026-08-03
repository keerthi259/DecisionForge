using Microsoft.Extensions.Options;

namespace DecisionForge.Api.Foundation;

public sealed class RequestBodyLimitMiddleware(
    RequestDelegate next,
    IOptions<ApiFoundationOptions> options)
{
    private readonly long _maximumBytes = options.Value.MaximumRequestBodyBytes;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(ApiRouteExtensions.VersionOnePrefix)
            || !context.Request.Body.CanRead
            || context.Request.ContentLength == 0)
        {
            await next(context);
            return;
        }

        if (context.Request.ContentLength > _maximumBytes)
        {
            throw new RequestBodyTooLargeException();
        }

        if (context.Request.ContentLength is null)
        {
            await BufferAndMeasureAsync(context);
        }

        await next(context);
    }

    private async Task BufferAndMeasureAsync(HttpContext context)
    {
        context.Request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: _maximumBytes + 1);
        byte[] buffer = new byte[16 * 1024];
        long total = 0;
        try
        {
            int read;
            while ((read = await context.Request.Body.ReadAsync(
                buffer,
                context.RequestAborted)) > 0)
            {
                total += read;
                if (total > _maximumBytes)
                {
                    throw new RequestBodyTooLargeException();
                }
            }
        }
        catch (IOException)
        {
            throw new RequestBodyTooLargeException();
        }
        finally
        {
            context.Request.Body.Position = 0;
        }
    }
}
