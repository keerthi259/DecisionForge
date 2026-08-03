using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Builders;

internal static class ApprovalWorkflowTestData
{
    public static readonly Guid WorkflowId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa0");
    public static readonly Guid DecisionId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb0");
    public static readonly Guid ActorId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-ccccccccccc0");

    public static ApprovalWorkflow Workflow(
        params PolicyApproverRole[] roles)
    {
        Decision decision = Decision(
            DecisionDisposition.ManualApprovalRequired,
            roles.Length == 0
                ? [
                    PolicyApproverRole.FinanceApprover,
                    PolicyApproverRole.DepartmentApprover,
                    PolicyApproverRole.SecurityApprover,
                ]
                : roles);
        return ApprovalWorkflow.Create(
            WorkflowId,
            decision,
            decision.RequiredApproverRoles.Select((_, index) => StageId(index)).ToArray(),
            decision.RequiredApproverRoles.Select((_, index) => Token(index)).ToArray(),
            PurchaseRequestBuilder.DefaultTime);
    }

    public static Decision Decision(
        DecisionDisposition disposition,
        IReadOnlyCollection<PolicyApproverRole>? roles = null)
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        string rolesJson = roles is null
            ? string.Empty
            : $",\"requiredApproverRoles\":[{string.Join(',', roles.Select(role => $"\"{role}\""))}]";
        PolicyDefinition definition = Parse(
            $$"""
            {
              "schemaVersion":"1.0",
              "policyCode":"APPROVAL-TEST",
              "name":"Approval test policy",
              "defaultOutcome":{
                "disposition":"{{disposition}}"{{rolesJson}},
                "reasonCode":"APPROVAL_TEST",
                "message":"Approval test outcome."
              },
              "rules":[]
            }
            """);
        PolicyEvaluationSource source = PolicyEvaluationSource.Create(
            Guid.Parse("dddddddd-dddd-4ddd-8ddd-ddddddddddd0"),
            Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee0"),
            PolicyVersionNumber.Create(1),
            PolicyStatus.Published,
            PolicyCanonicalSerializer.CalculateChecksum(definition),
            definition,
            PurchaseRequestBuilder.DefaultTime.AddDays(-1),
            null);
        PurchaseRequestEvaluationContext context = DecisionTestData.Context(request, source);
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            source.Definition,
            PolicyFactSet.FromSnapshot(context.NormalizedInput));
        return DecisionForge.Domain.Decisions.Decision.Create(
            DecisionId,
            request.Id,
            source,
            context,
            result,
            [],
            PurchaseRequestBuilder.DefaultTime);
    }

    public static PurchaseRequest PendingRequest()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        PurchaseRequestEvaluationContext context = DecisionTestData.Context(request);
        request.Submit(request.ConcurrencyToken, PurchaseRequestBuilder.Token(20), PurchaseRequestBuilder.DefaultTime);
        request.BeginEvaluation(context, request.ConcurrencyToken, PurchaseRequestBuilder.Token(21), PurchaseRequestBuilder.DefaultTime);
        request.CompleteEvaluation(
            DecisionDisposition.ManualApprovalRequired,
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(22),
            PurchaseRequestBuilder.DefaultTime);
        request.ClearDomainEvents();
        return request;
    }

    public static Guid StageId(int sequence)
    {
        return Guid.Parse($"aaaaaaaa-aaaa-4aaa-8aaa-{sequence + 1:000000000000}");
    }

    public static ConcurrencyToken Token(int sequence)
    {
        return ConcurrencyToken.Create(
            Guid.Parse($"ffffffff-ffff-4fff-8fff-{sequence + 1:000000000000}"));
    }

    private static PolicyDefinition Parse(string json)
    {
        PolicyParseResult parsed = PolicyJsonParser.Parse(json);
        return parsed.Definition ?? throw new InvalidOperationException(
            string.Join(Environment.NewLine, parsed.Errors.Select(error => $"{error.Path}: {error.Message}")));
    }
}
