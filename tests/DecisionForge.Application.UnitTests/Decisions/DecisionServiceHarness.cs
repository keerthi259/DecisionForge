using DecisionForge.Application.Decisions;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.UnitTests.Decisions;

internal sealed class DecisionServiceHarness
{
    public DecisionServiceHarness(
        PurchaseRequest request,
        PolicyEvaluationSource policy,
        StubEvaluationEngine? engine = null,
        params Guid[] generatedIds)
    {
        RequestRepository.Existing = request;
        Departments.Lookup = DecisionApplicationTestData.Department();
        Suppliers.Lookup = DecisionApplicationTestData.Supplier();
        PolicyQueries.Candidates = [policy];
        PolicyQueries.Exact = policy;
        Engine = engine ?? new StubEvaluationEngine();
        IdGenerator = new RequestSequenceIdGenerator(
            generatedIds.Length == 0 ? DefaultIds() : generatedIds);
        PurchaseRequestSubmissionPreconditionValidator validator = new(
            Departments,
            Suppliers,
            TimeProvider);
        Coordinator = new DecisionEvaluationCoordinator(
            validator,
            PolicyQueries,
            Engine);
        Persistence = new DecisionSubmissionPersistence(
            RequestRepository,
            IdempotencyStore,
            DecisionRepository,
            Transaction);
        Service = new DecisionSubmissionService(
            Persistence,
            Coordinator,
            CurrentUser,
            IdGenerator,
            TimeProvider);
    }

    public RecordingPurchaseRequestRepository RequestRepository { get; } = new();

    public RecordingSubmissionIdempotencyStore IdempotencyStore { get; } = new();

    public RecordingDecisionRepository DecisionRepository { get; } = new();

    public RecordingDecisionTransaction Transaction { get; } = new();

    public StubDepartmentQueries Departments { get; } = new();

    public StubSupplierQueries Suppliers { get; } = new();

    public StubPolicyDecisionQueries PolicyQueries { get; } = new();

    public StubEvaluationEngine Engine { get; }

    public StubCurrentUser CurrentUser { get; } = new(
        PurchaseRequestApplicationTestData.RequesterId);

    public RequestFixedTimeProvider TimeProvider { get; } = new(
        PurchaseRequestApplicationTestData.CurrentTime);

    public RequestSequenceIdGenerator IdGenerator { get; }

    public DecisionEvaluationCoordinator Coordinator { get; }

    public DecisionSubmissionPersistence Persistence { get; }

    public DecisionSubmissionService Service { get; }

    private static Guid[] DefaultIds()
    {
        return Enumerable.Range(100, 20)
            .Select(sequence => Guid.Parse(
                $"aaaaaaaa-aaaa-4aaa-8aaa-{sequence:000000000000}"))
            .ToArray();
    }
}
