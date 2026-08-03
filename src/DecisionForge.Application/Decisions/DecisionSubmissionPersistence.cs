using DecisionForge.Application.Decisions.Ports;
using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Application.PurchaseRequests.Ports;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Decisions;

public sealed class DecisionSubmissionPersistence
{
    private readonly IPurchaseRequestRepository _requestRepository;
    private readonly IPurchaseRequestSubmissionIdempotencyStore _idempotencyStore;
    private readonly IDecisionRepository _decisionRepository;
    private readonly IDecisionTransaction _transaction;

    public DecisionSubmissionPersistence(
        IPurchaseRequestRepository requestRepository,
        IPurchaseRequestSubmissionIdempotencyStore idempotencyStore,
        IDecisionRepository decisionRepository,
        IDecisionTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(requestRepository);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        ArgumentNullException.ThrowIfNull(decisionRepository);
        ArgumentNullException.ThrowIfNull(transaction);
        _requestRepository = requestRepository;
        _idempotencyStore = idempotencyStore;
        _decisionRepository = decisionRepository;
        _transaction = transaction;
    }

    internal Task<PurchaseRequest?> FindRequestAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        return _requestRepository.FindOwnedByIdAsync(
            purchaseRequestId,
            requesterId,
            cancellationToken);
    }

    internal Task<PurchaseRequestSubmissionRecord?> FindIdempotencyAsync(
        Guid requesterId,
        IdempotencyKey key,
        CancellationToken cancellationToken)
    {
        return _idempotencyStore.FindAsync(requesterId, key, cancellationToken);
    }

    internal Task<Decision?> FindDecisionAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        return _decisionRepository.FindOwnedByPurchaseRequestIdAsync(
            purchaseRequestId,
            requesterId,
            cancellationToken);
    }

    internal Task CommitDecisionAsync(
        PurchaseRequest purchaseRequest,
        Decision decision,
        ApprovalWorkflow? approvalWorkflow,
        PurchaseRequestSubmissionRecord? idempotencyRecord,
        CancellationToken cancellationToken)
    {
        return _transaction.CommitDecisionAsync(
            purchaseRequest,
            decision,
            approvalWorkflow,
            idempotencyRecord,
            cancellationToken);
    }

    internal Task CommitFailureAsync(
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken)
    {
        return _transaction.CommitEvaluationFailureAsync(
            purchaseRequest,
            cancellationToken);
    }
}
