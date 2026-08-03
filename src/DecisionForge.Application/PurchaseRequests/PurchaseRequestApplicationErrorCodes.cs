namespace DecisionForge.Application.PurchaseRequests;

public static class PurchaseRequestApplicationErrorCodes
{
    public const string Unauthenticated = "purchase-request.unauthenticated";
    public const string NotFound = "purchase-request.not-found";
    public const string ItemsRequired = "purchase-request.items-required";
    public const string SubmissionInvalidState = "purchase-request.submission-invalid-state";
    public const string ExpectedDeliveryDatePast = "purchase-request.expected-delivery-date-past";
    public const string DepartmentNotFound = "purchase-request.department-not-found";
    public const string DepartmentInactive = "purchase-request.department-inactive";
    public const string SupplierNotFound = "purchase-request.supplier-not-found";
    public const string SupplierInactive = "purchase-request.supplier-inactive";
    public const string DepartmentCurrencyMismatch = "purchase-request.department-currency-mismatch";
    public const string IdempotencyConflict = "purchase-request.idempotency-conflict";
    public const string EvaluationFailed = "purchase-request.evaluation-failed";
    public const string DecisionEvidenceUnavailable = "purchase-request.decision-evidence-unavailable";
}
