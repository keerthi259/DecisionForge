using Microsoft.AspNetCore.Diagnostics;

namespace DecisionForge.Api.Foundation;

public static class ApiStatusCodePages
{
    public static Task WriteAsync(StatusCodeContext statusCodeContext)
    {
        HttpContext context = statusCodeContext.HttpContext;
        (string Title, string Code) mapping = context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized =>
                ("Authentication is required.", ApiErrorCodes.AuthenticationRequired),
            StatusCodes.Status403Forbidden =>
                ("Access to the requested resource is denied.", ApiErrorCodes.AuthorizationDenied),
            StatusCodes.Status404NotFound =>
                ("The requested resource was not found.", ApiErrorCodes.NotFound),
            StatusCodes.Status405MethodNotAllowed =>
                ("The HTTP method is not supported for this resource.", ApiErrorCodes.MethodNotAllowed),
            StatusCodes.Status429TooManyRequests =>
                ("The request rate limit was exceeded.", ApiErrorCodes.RateLimit),
            _ => ("The request could not be completed.", "request.failed"),
        };

        return ApiProblemWriter.WriteAsync(
            context,
            context.Response.StatusCode,
            mapping.Title,
            mapping.Code,
            context.RequestAborted);
    }
}
