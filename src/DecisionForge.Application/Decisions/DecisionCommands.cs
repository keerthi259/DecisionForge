using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Decisions;

public sealed record SubmitPurchaseRequestForDecisionCommand(
    Guid PurchaseRequestId,
    ConcurrencyToken ExpectedToken,
    IdempotencyKey IdempotencyKey);

public sealed record RetryPurchaseRequestEvaluationCommand(
    Guid PurchaseRequestId,
    ConcurrencyToken ExpectedToken);
