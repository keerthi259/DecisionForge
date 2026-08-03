using DecisionForge.Application.Platform;
using DecisionForge.Application.Reliability;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions.Events;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Lifecycle.Events;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ReferenceData.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Reliability;

public sealed class ReliabilityEventMapperTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _aggregateId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _relatedId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid _thirdId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public void EveryCurrentDomainEventMapsToControlledAuditAndOutboxContracts()
    {
        ReliabilityEventMapper mapper = new(new SequentialIdGenerator());
        IDomainEvent[] events = AllEvents();

        ReliableEvent[] mapped = events.Select(@event => mapper.Map(
            @event,
            $"user:{_relatedId:D}",
            CorrelationId.Parse("corr-phase12"))).ToArray();

        Assert.Equal(events.Length, mapped.Length);
        Assert.All(mapped, item =>
        {
            Assert.Equal("decisionforge.domain-event.v1", item.Outbox.MessageType);
            Assert.Equal(item.Audit.EventType, item.Outbox.Payload.Fields["eventType"]);
            Assert.DoesNotContain("Sensitive approval note", item.Audit.Payload.Fields.Values);
            Assert.DoesNotContain("Sensitive approval note", item.Outbox.Payload.Fields.Values);
        });
        Assert.Equal(mapped.Length * 2, mapped.SelectMany(item =>
            new[] { item.Audit.EventId, item.Outbox.Id }).Distinct().Count());
    }

    [Fact]
    public void UnsupportedEventAndNullAreRejected()
    {
        ReliabilityEventMapper mapper = new(new SequentialIdGenerator());

        Assert.Throws<ArgumentException>(() => mapper.Map(
            new UnknownEvent(_now), "system", CorrelationId.Parse("corr")));
        Assert.Throws<ArgumentNullException>(() => mapper.Map(
            null!, "system", CorrelationId.Parse("corr")));
    }

    private static IDomainEvent[] AllEvents()
    {
        Money money = Money.Create(100m, CurrencyCode.Parse("INR"));
        PolicyChecksum checksum = PolicyChecksum.Parse(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        return
        [
            new PurchaseRequestCreatedDomainEvent(_aggregateId, _relatedId, _now),
            new PurchaseRequestClonedDomainEvent(_aggregateId, _relatedId, _thirdId, _now),
            new PurchaseRequestMetadataChangedDomainEvent(_aggregateId, _relatedId, _thirdId, _now),
            new PurchaseRequestItemAddedDomainEvent(_aggregateId, _relatedId, 2, money, _now),
            new PurchaseRequestItemChangedDomainEvent(_aggregateId, _relatedId, 3, money, _now),
            new PurchaseRequestItemRemovedDomainEvent(_aggregateId, _relatedId, _now),
            new PurchaseRequestSubmittedDomainEvent(_aggregateId, money, _now),
            new PurchaseRequestEvaluationStartedDomainEvent(
                _aggregateId, _relatedId, _thirdId, checksum, _now),
            new PurchaseRequestEvaluationCompletedDomainEvent(
                _aggregateId, DecisionDisposition.ManualApprovalRequired, _now),
            new PurchaseRequestEvaluationFailedDomainEvent(
                _aggregateId, ReasonCode.Parse("TECHNICAL_FAILURE"), _now),
            new PurchaseRequestEvaluationRetriedDomainEvent(_aggregateId, _now),
            new PurchaseRequestWithdrawnDomainEvent(_aggregateId, _now),
            new PurchaseRequestApprovalCompletedDomainEvent(
                _aggregateId, _relatedId, ApprovalOutcome.Approved, _now),
            new DecisionRecordedDomainEvent(
                _aggregateId, _relatedId, _thirdId, Guid.NewGuid(), checksum,
                DecisionDisposition.AutoApproved, _now),
            new ApprovalWorkflowCreatedDomainEvent(
                _aggregateId, _relatedId, _thirdId,
                [PolicyApproverRole.DepartmentApprover], _now),
            new ApprovalStageActivatedDomainEvent(
                _aggregateId, _relatedId, PolicyApproverRole.DepartmentApprover, _now),
            new ApprovalStageApprovedDomainEvent(
                _aggregateId, _relatedId, PolicyApproverRole.DepartmentApprover, _thirdId, _now),
            new ApprovalStageRejectedDomainEvent(
                _aggregateId, _relatedId, PolicyApproverRole.DepartmentApprover, _thirdId,
                "Sensitive approval note", _now),
            new ApprovalWorkflowCompletedDomainEvent(
                _aggregateId, _relatedId, ApprovalOutcome.Rejected, _now),
            new DecisionOverrideRecordedDomainEvent(
                _aggregateId, _relatedId, _thirdId,
                DecisionDisposition.ManualApprovalRequired, ApprovalOutcome.Approved,
                Guid.NewGuid(), "Sensitive approval note", _now),
            new PolicyCreatedDomainEvent(_aggregateId, PolicyCode.Parse("POLICY"), _now),
            new PolicyVersionDraftCreatedDomainEvent(
                _aggregateId, _relatedId, PolicyVersionNumber.Create(1), false, null, _now),
            new PolicyVersionDraftUpdatedDomainEvent(
                _aggregateId, _relatedId, PolicyVersionNumber.Create(1), true, checksum, _now),
            new PolicyVersionPublishedDomainEvent(
                _aggregateId, _relatedId, PolicyVersionNumber.Create(1), checksum,
                _now, _now.AddDays(1), _now),
            new PolicyVersionRetiredDomainEvent(
                _aggregateId, _relatedId, PolicyVersionNumber.Create(1),
                _now, null, _now),
            new DepartmentCreatedDomainEvent(
                _aggregateId, DepartmentCode.Parse("ENG"), _now),
            new DepartmentDetailsChangedDomainEvent(_aggregateId, money, _now),
            new DepartmentActivationChangedDomainEvent(_aggregateId, false, _now),
            new SupplierCreatedDomainEvent(
                _aggregateId, SupplierRegistrationNumber.Parse("REG-001"), _now),
            new SupplierDetailsChangedDomainEvent(
                _aggregateId, SupplierApprovalStatus.Approved,
                SupplierOnboardingStatus.Completed, SupplierRiskRating.Low, _now),
            new SupplierActivationChangedDomainEvent(_aggregateId, true, _now),
        ];
    }

    private sealed class SequentialIdGenerator : IIdGenerator
    {
        private long _value;

        public Guid Create()
        {
            _value++;
            Span<byte> bytes = stackalloc byte[16];
            BitConverter.TryWriteBytes(bytes, _value);
            bytes[8] = 0x80;
            return new Guid(bytes);
        }
    }

    private sealed record UnknownEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
