using System.Diagnostics;

namespace DecisionForge.Api.Foundation;

public sealed record ApiValidationError(string Code, string Path, string Message);

public sealed record ApiProblemDetails(
    string Type,
    string Title,
    int Status,
    string ErrorCode,
    string TraceId,
    string? Detail = null,
    string? Instance = null,
    IReadOnlyList<ApiValidationError>? Errors = null)
{
    public static ApiProblemDetails Create(
        HttpContext context,
        int status,
        string title,
        string errorCode,
        string? detail = null,
        IReadOnlyList<ApiValidationError>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new ApiProblemDetails(
            $"https://decisionforge.local/problems/{errorCode}",
            title,
            status,
            errorCode,
            Activity.Current?.Id ?? context.TraceIdentifier,
            detail,
            context.Request.Path,
            errors);
    }
}
