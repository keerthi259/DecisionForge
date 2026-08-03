using DecisionForge.Application.Decisions.Ports;
using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Application.PurchaseRequests.Ports;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Decisions;

internal sealed class StubPolicyDecisionQueries : IPolicyDecisionQueries
{
    public IReadOnlyList<PolicyEvaluationSource> Candidates { get; set; } = [];

    public PolicyEvaluationSource? Exact { get; set; }

    public int ListCalls { get; private set; }

    public int ExactCalls { get; private set; }

    public DateTimeOffset? RequestedTimestamp { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<IReadOnlyList<PolicyEvaluationSource>> ListCandidatesAtAsync(
        DateTimeOffset submissionTimestamp,
        CancellationToken cancellationToken)
    {
        ListCalls++;
        RequestedTimestamp = submissionTimestamp;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Candidates);
    }

    public Task<PolicyEvaluationSource?> FindByVersionIdAsync(
        Guid policyVersionId,
        CancellationToken cancellationToken)
    {
        ExactCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Exact?.VersionId == policyVersionId ? Exact : null);
    }
}

internal sealed class StubEvaluationEngine : IPolicyEvaluationEngine
{
    public Exception? Failure { get; set; }

    public PolicyDefinition? ReplacementPolicy { get; set; }

    public int Calls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public PolicyEvaluationResult Evaluate(
        PolicyDefinition policy,
        PolicyFactSet facts,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastCancellationToken = cancellationToken;
        if (Failure is not null)
        {
            throw Failure;
        }

        return PolicyEvaluator.Evaluate(ReplacementPolicy ?? policy, facts, cancellationToken);
    }
}

internal sealed class RecordingSubmissionIdempotencyStore
    : IPurchaseRequestSubmissionIdempotencyStore
{
    public PurchaseRequestSubmissionRecord? Existing { get; set; }

    public int FindCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<PurchaseRequestSubmissionRecord?> FindAsync(
        Guid requesterId,
        IdempotencyKey key,
        CancellationToken cancellationToken)
    {
        FindCalls++;
        LastCancellationToken = cancellationToken;
        PurchaseRequestSubmissionRecord? result = Existing is not null
            && Existing.RequesterId == requesterId
            && Existing.Key == key
                ? Existing
                : null;
        return Task.FromResult(result);
    }

}

internal sealed class RecordingDecisionRepository : IDecisionRepository
{
    public Decision? Existing { get; set; }

    public Guid AllowedRequesterId { get; set; } =
        PurchaseRequestApplicationTestData.RequesterId;

    public int Calls { get; private set; }

    public Guid? RequesterId { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<Decision?> FindOwnedByPurchaseRequestIdAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        Calls++;
        RequesterId = requesterId;
        LastCancellationToken = cancellationToken;
        Decision? result = Existing?.PurchaseRequestId == purchaseRequestId
            && requesterId == AllowedRequesterId
                ? Existing
                : null;
        return Task.FromResult(result);
    }
}

internal sealed class RecordingDecisionTransaction : IDecisionTransaction
{
    public PurchaseRequest? Request { get; private set; }

    public Decision? Decision { get; private set; }

    public ApprovalWorkflow? ApprovalWorkflow { get; private set; }

    public PurchaseRequestSubmissionRecord? IdempotencyRecord { get; private set; }

    public int DecisionCommits { get; private set; }

    public int FailureCommits { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task CommitDecisionAsync(
        PurchaseRequest purchaseRequest,
        Decision decision,
        ApprovalWorkflow? approvalWorkflow,
        PurchaseRequestSubmissionRecord? idempotencyRecord,
        CancellationToken cancellationToken)
    {
        DecisionCommits++;
        Request = purchaseRequest;
        Decision = decision;
        ApprovalWorkflow = approvalWorkflow;
        IdempotencyRecord = idempotencyRecord;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task CommitEvaluationFailureAsync(
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken)
    {
        FailureCommits++;
        Request = purchaseRequest;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal static class DecisionApplicationTestData
{
    public static readonly Guid PolicyId =
        Guid.Parse("77777777-7777-4777-8777-777777777777");
    public static readonly Guid PolicyVersionId =
        Guid.Parse("88888888-8888-4888-8888-888888888888");

    public static DepartmentLookup Department()
    {
        return new DepartmentLookup(
            PurchaseRequestApplicationTestData.DepartmentId,
            DepartmentCode.Parse("ENG"),
            "Engineering",
            Money.Create(250_000m, PurchaseRequestApplicationTestData.Currency),
            true);
    }

    public static SupplierLookup Supplier()
    {
        return new SupplierLookup(
            PurchaseRequestApplicationTestData.SupplierId,
            SupplierRegistrationNumber.Parse("SUP-001"),
            "Global Technology Systems",
            SupplierApprovalStatus.Approved,
            SupplierOnboardingStatus.Completed,
            SupplierRiskRating.Medium,
            true);
    }

    public static PolicyEvaluationSource Source(
        PolicyDefinition? definition = null,
        Guid? policyId = null,
        Guid? versionId = null)
    {
        PolicyDefinition selected = definition ?? FlagshipPolicy();
        return PolicyEvaluationSource.Create(
            policyId ?? PolicyId,
            versionId ?? PolicyVersionId,
            PolicyVersionNumber.Create(3),
            PolicyStatus.Published,
            PolicyCanonicalSerializer.CalculateChecksum(selected),
            selected,
            PurchaseRequestApplicationTestData.InitialTime.AddDays(-1),
            null);
    }

    public static PolicyDefinition FlagshipPolicy()
    {
        return Parse(
            """
            {
              "schemaVersion":"1.0",
              "policyCode":"PROCUREMENT-GLOBAL",
              "name":"Global Procurement Policy",
              "defaultOutcome":{"disposition":"AutoApproved","reasonCode":"DEFAULT","message":"Default."},
              "rules":[
                {
                  "id":"HIGH-VALUE","priority":10,
                  "when":{"fact":"request.totalAmount","operator":"greaterThanOrEqual","value":2000},
                  "then":{"disposition":"ManualApprovalRequired","requiredApproverRoles":["FinanceApprover"],"reasonCode":"HIGH_VALUE","message":"Finance review required."}
                },
                {
                  "id":"TECHNOLOGY","priority":20,
                  "when":{"fact":"derived.containsTechnologyPurchase","operator":"equals","value":true},
                  "then":{"disposition":"ManualApprovalRequired","requiredApproverRoles":["SecurityApprover"],"reasonCode":"TECHNOLOGY","message":"Security review required."}
                }
              ]
            }
            """);
    }

    public static PolicyDefinition RejectionPolicy()
    {
        return Parse(
            """
            {
              "schemaVersion":"1.0",
              "policyCode":"PROCUREMENT-GLOBAL",
              "name":"Global Procurement Policy",
              "defaultOutcome":{"disposition":"Rejected","reasonCode":"REPRO_CHANGED","message":"Changed."},
              "rules":[]
            }
            """);
    }

    public static PolicyDefinition AutoApprovalPolicy()
    {
        return Parse(
            """
            {
              "schemaVersion":"1.0",
              "policyCode":"PROCUREMENT-GLOBAL",
              "name":"Global Procurement Policy",
              "defaultOutcome":{"disposition":"AutoApproved","reasonCode":"STANDARD","message":"Standard."},
              "rules":[]
            }
            """);
    }

    private static PolicyDefinition Parse(string json)
    {
        PolicyParseResult parsed = PolicyJsonParser.Parse(json);
        return parsed.Definition!;
    }
}
