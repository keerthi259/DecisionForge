namespace DecisionForge.Api.Foundation;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            HttpResponse response = (HttpResponse)state;
            response.Headers["Content-Security-Policy"] =
                "default-src 'self'; base-uri 'self'; frame-ancestors 'none'; object-src 'none'";
            response.Headers["Permissions-Policy"] =
                "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
            response.Headers["Referrer-Policy"] = "no-referrer";
            response.Headers["X-Content-Type-Options"] = "nosniff";
            response.Headers["X-Frame-Options"] = "DENY";
            return Task.CompletedTask;
        }, context.Response);

        return next(context);
    }
}
