using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.Decisions.Ports;

public interface IDecisionTransaction
{
    Task CommitDecisionAsync(
        PurchaseRequest purchaseRequest,
        Decision decision,
        ApprovalWorkflow? approvalWorkflow,
        PurchaseRequestSubmissionRecord? idempotencyRecord,
        CancellationToken cancellationToken);

    Task CommitEvaluationFailureAsync(
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken);
}
