namespace DecisionForge.Api.Foundation;

public static class ApiErrorCodes
{
    public const string AuthenticationRequired = "authentication.required";
    public const string AuthorizationDenied = "authorization.denied";
    public const string BodyTooLarge = "request.body-too-large";
    public const string ConcurrencyConflict = "concurrency.conflict";
    public const string DuplicateOperation = "duplicate.operation";
    public const string IdempotencyConflict = "idempotency.conflict";
    public const string IdempotencyInProgress = "idempotency.in-progress";
    public const string IdempotencyKeyRequired = "idempotency.key-required";
    public const string InternalError = "internal.error";
    public const string InvalidState = "state.invalid";
    public const string MethodNotAllowed = "request.method-not-allowed";
    public const string NotFound = "resource.not-found";
    public const string PreconditionRequired = "concurrency.precondition-required";
    public const string RateLimit = "rate-limit.exceeded";
    public const string ValidationBusiness = "validation.business";
    public const string ValidationField = "validation.field";
}
