namespace DecisionForge.Api.Foundation;

public static class ApiProblemWriter
{
    public static IResult Result(
        HttpContext context,
        int status,
        string title,
        string errorCode,
        string? detail = null,
        IReadOnlyList<ApiValidationError>? errors = null)
    {
        return Results.Json(
            ApiProblemDetails.Create(context, status, title, errorCode, detail, errors),
            statusCode: status,
            contentType: "application/problem+json");
    }

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string errorCode,
        CancellationToken cancellationToken,
        string? detail = null,
        IReadOnlyList<ApiValidationError>? errors = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Results.Json(
            ApiProblemDetails.Create(context, status, title, errorCode, detail, errors),
            statusCode: status,
            contentType: "application/problem+json").ExecuteAsync(context);
    }
}
