using DecisionForge.Application.Platform;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Decisions;

public sealed class DecisionSubmissionService
{
    private static readonly ReasonCode _technicalFailureReason =
        ReasonCode.Parse("EVALUATION_TECHNICAL_FAILURE");

    private readonly DecisionSubmissionPersistence _persistence;
    private readonly DecisionEvaluationCoordinator _coordinator;
    private readonly ICurrentUserContext _currentUser;
    private readonly IIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public DecisionSubmissionService(
        DecisionSubmissionPersistence persistence,
        DecisionEvaluationCoordinator coordinator,
        ICurrentUserContext currentUser,
        IIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _persistence = persistence;
        _coordinator = coordinator;
        _currentUser = currentUser;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<DecisionSubmissionResult> SubmitAsync(
        SubmitPurchaseRequestForDecisionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.IdempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        SubmissionFingerprint fingerprint = SubmissionFingerprintBuilder.Build(command);
        DecisionSubmissionResult? replay = await ResolveReplayAsync(
            requesterId,
            command.IdempotencyKey,
            fingerprint,
            cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        PurchaseRequest request = await FindRequestAsync(
            command.PurchaseRequestId,
            requesterId,
            cancellationToken);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        PreparedDecisionEvaluation prepared = await _coordinator.PrepareInitialAsync(
            request,
            occurredAt,
            cancellationToken);
        request.Submit(command.ExpectedToken, NextToken(), occurredAt);
        request.BeginEvaluation(
            prepared.Context,
            request.ConcurrencyToken,
            NextToken(),
            occurredAt);
        SubmissionIdentity identity = new(
            requesterId,
            command.IdempotencyKey,
            fingerprint);
        Decision decision = await EvaluateAndCommitAsync(
            request,
            prepared,
            identity,
            occurredAt,
            cancellationToken);
        return new DecisionSubmissionResult(decision, false);
    }

    public async Task<DecisionSubmissionResult> RetryAsync(
        RetryPurchaseRequestEvaluationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        PurchaseRequest request = await FindRequestAsync(
            command.PurchaseRequestId,
            requesterId,
            cancellationToken);
        PreparedDecisionEvaluation prepared = await _coordinator.PrepareRetryAsync(
            request,
            cancellationToken);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        request.RetryEvaluation(command.ExpectedToken, NextToken(), occurredAt);
        request.BeginEvaluation(
            prepared.Context,
            request.ConcurrencyToken,
            NextToken(),
            occurredAt);
        Decision decision = await EvaluateAndCommitAsync(
            request,
            prepared,
            null,
            occurredAt,
            cancellationToken);
        return new DecisionSubmissionResult(decision, false);
    }

    private async Task<PolicyEvaluationResult?> EvaluateAsync(
        PurchaseRequest request,
        PreparedDecisionEvaluation prepared,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        try
        {
            return _coordinator.Evaluate(prepared, cancellationToken);
        }
        catch (Exception exception) when (IsTechnicalEvaluationFailure(exception))
        {
            request.MarkEvaluationFailed(
                _technicalFailureReason,
                request.ConcurrencyToken,
                NextToken(),
                occurredAt);
            await _persistence.CommitFailureAsync(request, cancellationToken);
            return null;
        }
    }

    private async Task<Decision> EvaluateAndCommitAsync(
        PurchaseRequest request,
        PreparedDecisionEvaluation prepared,
        SubmissionIdentity? submissionIdentity,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        PolicyEvaluationResult? result = await EvaluateAsync(
            request,
            prepared,
            occurredAt,
            cancellationToken);
        if (result is null)
        {
            throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.EvaluationFailed,
                "Policy evaluation failed. The request is safe to retry.");
        }

        Guid[] ruleIds = result.Rules.Select(_ => _idGenerator.Create()).ToArray();
        Decision decision = Decision.Create(
            _idGenerator.Create(),
            request.Id,
            prepared.Policy,
            prepared.Context,
            result,
            ruleIds,
            occurredAt);
        ApprovalWorkflow? approvalWorkflow = CreateApprovalWorkflow(decision, occurredAt);

        request.CompleteEvaluation(
            decision.Disposition,
            request.ConcurrencyToken,
            NextToken(),
            occurredAt);
        PurchaseRequestSubmissionRecord? record = submissionIdentity?.CreateRecord(
            request.Id,
            occurredAt);
        await _persistence.CommitDecisionAsync(
            request,
            decision,
            approvalWorkflow,
            record,
            cancellationToken);
        return decision;
    }

    private async Task<DecisionSubmissionResult?> ResolveReplayAsync(
        Guid requesterId,
        IdempotencyKey key,
        SubmissionFingerprint fingerprint,
        CancellationToken cancellationToken)
    {
        PurchaseRequestSubmissionRecord? existing = await _persistence.FindIdempotencyAsync(
            requesterId,
            key,
            cancellationToken);
        SubmissionIdempotencyResolution resolution =
            PurchaseRequestSubmissionIdempotency.Resolve(existing, fingerprint);
        if (!resolution.IsReplay)
        {
            return null;
        }

        Decision? decision = await _persistence.FindDecisionAsync(
            resolution.OriginalPurchaseRequestId!.Value,
            requesterId,
            cancellationToken);
        return decision is null
            ? throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.DecisionEvidenceUnavailable,
                "The original decision evidence is unavailable for replay.")
            : new DecisionSubmissionResult(decision, true);
    }

    private async Task<PurchaseRequest> FindRequestAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        PurchaseRequest? request = await _persistence.FindRequestAsync(
            purchaseRequestId,
            requesterId,
            cancellationToken);
        return request
            ?? throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.NotFound,
                $"Purchase request '{purchaseRequestId}' was not found.",
                nameof(purchaseRequestId));
    }

    private static bool IsTechnicalEvaluationFailure(Exception exception)
    {
        return exception is not OperationCanceledException
            and not DomainRuleException;
    }

    private ConcurrencyToken NextToken()
    {
        return ConcurrencyToken.Create(_idGenerator.Create());
    }

    private ApprovalWorkflow? CreateApprovalWorkflow(
        Decision decision,
        DateTimeOffset occurredAt)
    {
        if (decision.Disposition != DecisionDisposition.ManualApprovalRequired)
        {
            return null;
        }

        Guid[] stageIds = decision.RequiredApproverRoles
            .Select(_ => _idGenerator.Create())
            .ToArray();
        ConcurrencyToken[] stageTokens = decision.RequiredApproverRoles
            .Select(_ => NextToken())
            .ToArray();
        return ApprovalWorkflow.Create(
            _idGenerator.Create(),
            decision,
            stageIds,
            stageTokens,
            occurredAt);
    }

    private sealed record SubmissionIdentity(
        Guid RequesterId,
        IdempotencyKey Key,
        SubmissionFingerprint Fingerprint)
    {
        public PurchaseRequestSubmissionRecord CreateRecord(
            Guid purchaseRequestId,
            DateTimeOffset completedAt)
        {
            return PurchaseRequestSubmissionRecord.Create(
                RequesterId,
                Key,
                Fingerprint,
                purchaseRequestId,
                completedAt);
        }
    }
}
