using DecisionForge.Application.Approvals;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Lifecycle;
using Microsoft.AspNetCore.Diagnostics;

namespace DecisionForge.Api.Foundation;

public sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        ApiExceptionMapping mapping = Map(exception);
        if (!mapping.IsExpected)
        {
            LogUnhandledException(logger, exception, httpContext.TraceIdentifier);
        }

        await ApiProblemWriter.WriteAsync(
            httpContext,
            mapping.Status,
            mapping.Title,
            mapping.ErrorCode,
            cancellationToken,
            mapping.Detail,
            mapping.Errors);
        return true;
    }

    private static ApiExceptionMapping Map(Exception exception)
    {
        return exception switch
        {
            ApiRequestValidationException validation => new(
                StatusCodes.Status400BadRequest,
                "The request contains invalid fields.",
                ApiErrorCodes.ValidationField,
                true,
                Errors: validation.Errors),
            ApiPreconditionException precondition => new(
                precondition.Status,
                precondition.Message,
                precondition.Code,
                true),
            RequestBodyTooLargeException => new(
                StatusCodes.Status413PayloadTooLarge,
                "The request body is too large.",
                ApiErrorCodes.BodyTooLarge,
                true),
            BadHttpRequestException badRequest => MapBadRequest(badRequest),
            SubmissionPreconditionException submission => new(
                StatusCodes.Status422UnprocessableEntity,
                "Business validation failed.",
                ApiErrorCodes.ValidationBusiness,
                true,
                Errors: submission.Errors.Select(error =>
                    new ApiValidationError(error.Code, error.Path, error.Message)).ToArray()),
            DomainRuleException domain => MapDomainRule(domain),
            PolicyEvaluationException policy => new(
                StatusCodes.Status422UnprocessableEntity,
                "Policy evaluation failed.",
                policy.Code,
                true,
                Errors: [new ApiValidationError(policy.Code, policy.Path, policy.Message)]),
            _ => new(
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                ApiErrorCodes.InternalError,
                false),
        };
    }

    private static ApiExceptionMapping MapBadRequest(BadHttpRequestException exception)
    {
        if (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return new ApiExceptionMapping(
                StatusCodes.Status413PayloadTooLarge,
                "The request body is too large.",
                ApiErrorCodes.BodyTooLarge,
                true);
        }

        return new ApiExceptionMapping(
            StatusCodes.Status400BadRequest,
            "The request body is invalid.",
            ApiErrorCodes.ValidationField,
            true,
            Errors:
            [
                new ApiValidationError(
                    "request.body.invalid",
                    "$",
                    "The request body is malformed or does not match the contract."),
            ]);
    }

    private static ApiExceptionMapping MapDomainRule(DomainRuleException exception)
    {
        return exception.Code switch
        {
            ApprovalApplicationErrorCodes.Unauthenticated
                or PurchaseRequestApplicationErrorCodes.Unauthenticated => Expected(
                    StatusCodes.Status401Unauthorized,
                    "Authentication is required.",
                    ApiErrorCodes.AuthenticationRequired),
            ApprovalApplicationErrorCodes.Forbidden or ApprovalErrorCodes.RoleMismatch => Expected(
                StatusCodes.Status403Forbidden,
                "Access to the requested resource is denied.",
                ApiErrorCodes.AuthorizationDenied),
            DomainErrorCodes.ConcurrencyConflict => Expected(
                StatusCodes.Status412PreconditionFailed,
                "The resource changed after it was read.",
                ApiErrorCodes.ConcurrencyConflict),
            PurchaseRequestApplicationErrorCodes.IdempotencyConflict => Expected(
                StatusCodes.Status409Conflict,
                "The idempotency key was used with different input.",
                ApiErrorCodes.IdempotencyConflict),
            DomainErrorCodes.EntityNotFound or PurchaseRequestApplicationErrorCodes.NotFound
                or ApprovalApplicationErrorCodes.NotFound or ApprovalErrorCodes.StageNotFound => Expected(
                    StatusCodes.Status404NotFound,
                    "The requested resource was not found.",
                    ApiErrorCodes.NotFound),
            DomainErrorCodes.DuplicateEntity => Expected(
                StatusCodes.Status409Conflict,
                "The operation would create a duplicate resource.",
                ApiErrorCodes.DuplicateOperation),
            DomainErrorCodes.InvalidState or ApprovalErrorCodes.NotActionable
                or PolicyLifecycleErrorCodes.ImmutableVersion => Expected(
                StatusCodes.Status409Conflict,
                "The resource is not in a valid state for this operation.",
                ApiErrorCodes.InvalidState),
            DecisionErrorCodes.NoEffectivePolicy => Expected(
                StatusCodes.Status422UnprocessableEntity,
                "No effective policy is available.",
                DecisionErrorCodes.NoEffectivePolicy),
            _ => new ApiExceptionMapping(
                StatusCodes.Status422UnprocessableEntity,
                "Business validation failed.",
                ApiErrorCodes.ValidationBusiness,
                true,
                Errors:
                [
                    new ApiValidationError(
                        exception.Code,
                        exception.ParameterName ?? "$",
                        exception.Message),
                ]),
        };
    }

    private static ApiExceptionMapping Expected(int status, string title, string code)
    {
        return new ApiExceptionMapping(status, title, code, true);
    }

    [LoggerMessage(
        EventId = 14001,
        Level = LogLevel.Error,
        Message = "Unhandled API exception. TraceId: {traceId}.")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string traceId);

    private sealed record ApiExceptionMapping(
        int Status,
        string Title,
        string ErrorCode,
        bool IsExpected,
        string? Detail = null,
        IReadOnlyList<ApiValidationError>? Errors = null);
}
