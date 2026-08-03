using DecisionForge.Application.Approvals.Auditing;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;

namespace DecisionForge.Application.UnitTests.Approvals;

public sealed class ApprovalAuditMapperTests
{
    private static readonly Guid _workflowId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _requestId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid _decisionId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid _stageId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid _actorId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset _time = new(2026, 8, 3, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OverrideMappingPreservesOriginalOutcomePermissionActorAndReasonEvidence()
    {
        ApprovalAuditRecord record = ApprovalAuditMapper.Map(
            new DecisionOverrideRecordedDomainEvent(
                _workflowId,
                _requestId,
                _decisionId,
                DecisionDisposition.ManualApprovalRequired,
                ApprovalOutcome.Approved,
                _actorId,
                "Emergency authorization.",
                _time));

        Assert.Equal("ApprovalWorkflow", record.AggregateType);
        Assert.Equal("decision.overridden", record.EventType);
        Assert.Equal("ManualApprovalRequired", record.Fields["originalDisposition"]);
        Assert.Equal("Approved", record.Fields["outcome"]);
        Assert.Equal("true", record.Fields["noteProvided"]);
        Assert.Equal("24", record.Fields["noteLength"]);
        Assert.Equal(64, record.Fields["noteSha256"].Length);
        Assert.DoesNotContain("Emergency authorization.", record.Fields.Values);
        Assert.Equal(_actorId.ToString("D"), record.Fields["actorId"]);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)record.Fields).Clear());
    }

    [Fact]
    public void EveryApprovalEventMapsToAControlledEventType()
    {
        IDomainEvent[] events =
        [
            new ApprovalWorkflowCreatedDomainEvent(
                _workflowId,
                _requestId,
                _decisionId,
                [PolicyApproverRole.FinanceApprover],
                _time),
            new ApprovalStageActivatedDomainEvent(
                _workflowId,
                _stageId,
                PolicyApproverRole.FinanceApprover,
                _time),
            new ApprovalStageApprovedDomainEvent(
                _workflowId,
                _stageId,
                PolicyApproverRole.FinanceApprover,
                _actorId,
                _time),
            new ApprovalStageRejectedDomainEvent(
                _workflowId,
                _stageId,
                PolicyApproverRole.FinanceApprover,
                _actorId,
                "Rejected.",
                _time),
            new ApprovalWorkflowCompletedDomainEvent(
                _workflowId,
                _requestId,
                ApprovalOutcome.Rejected,
                _time),
        ];

        Assert.Equal(
        [
            "approval-workflow.created",
            "approval-stage.activated",
            "approval-stage.approved",
            "approval-stage.rejected",
            "approval-workflow.completed",
        ],
            events.Select(@event => ApprovalAuditMapper.Map(@event).EventType));
        Assert.Throws<ArgumentException>(() => ApprovalAuditMapper.Map(new UnknownEvent(_time)));
    }

    private sealed record UnknownEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
