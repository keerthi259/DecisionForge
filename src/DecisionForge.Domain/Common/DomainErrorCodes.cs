namespace DecisionForge.Domain.Common;

public static class DomainErrorCodes
{
    public const string Validation = "domain.validation";
    public const string InvalidState = "domain.invalid-state";
    public const string EntityNotFound = "domain.entity-not-found";
    public const string DuplicateEntity = "domain.duplicate-entity";
    public const string CurrencyMismatch = "domain.currency-mismatch";
    public const string AmountOverflow = "domain.amount-overflow";
    public const string ConcurrencyConflict = "domain.concurrency-conflict";
    public const string InactiveReference = "domain.inactive-reference";
    public const string ReferenceMismatch = "domain.reference-mismatch";
}
