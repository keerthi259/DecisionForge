using DecisionForge.Application.Policies.Auditing;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Policies;

public sealed class PolicyLifecycleAuditMapperTests
{
    [Fact]
    public void EveryLifecycleEventMapsToControlledSafeAuditData()
    {
        Policy policy = PolicyApplicationTestData.Existing();
        PolicyVersion first = Assert.Single(policy.Versions);
        policy.Publish(
            first.Id,
            PolicyApplicationTestData.Now.AddMinutes(1),
            null,
            policy.ConcurrencyToken,
            ConcurrencyToken.Create(PolicyApplicationTestData.NextTokenId),
            PolicyApplicationTestData.Now);
        policy.Retire(
            first.Id,
            policy.ConcurrencyToken,
            ConcurrencyToken.Create(PolicyApplicationTestData.ThirdTokenId),
            PolicyApplicationTestData.Now.AddMinutes(2));

        PolicyLifecycleAuditRecord[] records = policy.DomainEvents
            .Select(PolicyLifecycleAuditMapper.Map)
            .ToArray();

        Assert.Equal(
            [
                "policy.created",
                "policy-version.draft-created",
                "policy-version.published",
                "policy-version.retired",
            ],
            records.Select(record => record.EventType));
        Assert.All(records, record =>
        {
            Assert.Equal(policy.Id, record.AggregateId);
            Assert.Equal("Policy", record.AggregateType);
            Assert.DoesNotContain(
                record.Fields,
                field => field.Value.Contains("schemaVersion", StringComparison.Ordinal));
        });
        Assert.Equal(first.Checksum!.Value, records[2].Fields["checksum"]);
        Assert.Equal("1", records[2].Fields["version"]);
    }

    [Fact]
    public void AuditFieldsAreImmutableAndUnsupportedEventsFail()
    {
        PolicyLifecycleAuditRecord record = PolicyLifecycleAuditMapper.Map(
            Assert.Single(PolicyApplicationTestData.Existing().DomainEvents.OfType<
                DecisionForge.Domain.Policies.Lifecycle.Events.PolicyCreatedDomainEvent>()));
        IDictionary<string, string> fields =
            Assert.IsAssignableFrom<IDictionary<string, string>>(record.Fields);

        Assert.Throws<NotSupportedException>(() => fields.Add("definitionJson", "secret"));
        Assert.Throws<ArgumentException>(() => PolicyLifecycleAuditMapper.Map(
            new UnrelatedDomainEvent(PolicyApplicationTestData.Now)));
        Assert.Throws<ArgumentNullException>(() => PolicyLifecycleAuditMapper.Map(null!));
    }

    [Fact]
    public void DraftUpdateAndBoundedPublicationMapEveryOptionalField()
    {
        Policy policy = PolicyApplicationTestData.Existing("{ invalid");
        PolicyVersion version = Assert.Single(policy.Versions);
        PolicyLifecycleAuditRecord invalidDraft = PolicyLifecycleAuditMapper.Map(
            policy.DomainEvents[1]);
        policy.ClearDomainEvents();
        policy.UpdateDraft(
            version.Id,
            PolicyApplicationTestData.ValidJson,
            policy.ConcurrencyToken,
            ConcurrencyToken.Create(PolicyApplicationTestData.NextTokenId),
            PolicyApplicationTestData.Now.AddSeconds(1));
        PolicyLifecycleAuditRecord updated = PolicyLifecycleAuditMapper.Map(
            Assert.Single(policy.DomainEvents));
        policy.ClearDomainEvents();
        policy.Publish(
            version.Id,
            PolicyApplicationTestData.Now.AddMinutes(1),
            PolicyApplicationTestData.Now.AddHours(1),
            policy.ConcurrencyToken,
            ConcurrencyToken.Create(PolicyApplicationTestData.ThirdTokenId),
            PolicyApplicationTestData.Now.AddSeconds(2));
        PolicyLifecycleAuditRecord published = PolicyLifecycleAuditMapper.Map(
            Assert.Single(policy.DomainEvents));

        Assert.Equal("false", invalidDraft.Fields["isValid"]);
        Assert.DoesNotContain("checksum", invalidDraft.Fields.Keys);
        Assert.Equal("policy-version.draft-updated", updated.EventType);
        Assert.Equal(version.Checksum!.Value, updated.Fields["checksum"]);
        Assert.Equal(
            PolicyApplicationTestData.Now.AddHours(1).ToString("O"),
            published.Fields["effectiveUntil"]);
    }

    private sealed record UnrelatedDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
