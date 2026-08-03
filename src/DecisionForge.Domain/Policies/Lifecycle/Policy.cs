using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Lifecycle.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Lifecycle;

public sealed class Policy : AggregateRoot
{
    private readonly List<PolicyVersion> _versions;
    private readonly ReadOnlyCollection<PolicyVersion> _versionsView;

    private Policy(
        Guid id,
        PolicyCode code,
        string name,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        Name = name;
        ConcurrencyToken = concurrencyToken;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
        _versions = [];
        _versionsView = _versions.AsReadOnly();
    }

    public PolicyCode Code { get; }

    public string Name { get; }

    public ConcurrencyToken ConcurrencyToken { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public IReadOnlyList<PolicyVersion> Versions => _versionsView;

    public static Policy Create(
        Guid id,
        Guid initialVersionId,
        PolicyCode code,
        string name,
        string? definitionJson,
        ConcurrencyToken concurrencyToken,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(concurrencyToken);
        string normalizedName = PolicyLifecycleGuard.Name(name);
        DateTimeOffset utcCreatedAt = DomainGuard.Utc(createdAt, nameof(createdAt));
        Policy policy = new(
            id,
            code,
            normalizedName,
            concurrencyToken,
            utcCreatedAt);
        PolicyVersion version = PolicyVersion.CreateDraft(
            initialVersionId,
            PolicyVersionNumber.Create(1),
            code,
            normalizedName,
            definitionJson,
            utcCreatedAt);
        policy._versions.Add(version);
        policy.Raise(new PolicyCreatedDomainEvent(id, code, utcCreatedAt));
        policy.Raise(DraftCreated(policy, version, utcCreatedAt));
        return policy;
    }

    public PolicyVersion CreateDraft(
        Guid versionId,
        string? definitionJson,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = Mutation(
            expectedToken,
            nextToken,
            occurredAt);
        if (_versions.Any(version => version.Status == PolicyStatus.Draft))
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                "A policy can have only one draft version at a time.");
        }

        if (_versions.Any(version => version.Id == versionId))
        {
            throw new DomainRuleException(
                DomainErrorCodes.DuplicateEntity,
                "The policy version identifier already exists.",
                nameof(versionId));
        }

        PolicyVersionNumber nextNumber = _versions[^1].Number.Next();
        PolicyVersion version = PolicyVersion.CreateDraft(
            versionId,
            nextNumber,
            Code,
            Name,
            definitionJson,
            utcOccurredAt);
        _versions.Add(version);
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(DraftCreated(this, version, utcOccurredAt));
        return version;
    }

    public PolicyVersion UpdateDraft(
        Guid versionId,
        string? definitionJson,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = Mutation(
            expectedToken,
            nextToken,
            occurredAt);
        PolicyVersion version = FindVersion(versionId);
        if (!version.UpdateDraft(Code, Name, definitionJson, utcOccurredAt))
        {
            return version;
        }

        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PolicyVersionDraftUpdatedDomainEvent(
            Id,
            version.Id,
            version.Number,
            version.IsValid,
            version.Checksum,
            utcOccurredAt));
        return version;
    }

    public PolicyVersion Publish(
        Guid versionId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = Mutation(
            expectedToken,
            nextToken,
            occurredAt);
        DateTimeOffset utcEffectiveFrom = DomainGuard.Utc(
            effectiveFrom,
            nameof(effectiveFrom));
        DateTimeOffset? utcEffectiveUntil = effectiveUntil is null
            ? null
            : DomainGuard.Utc(effectiveUntil.Value, nameof(effectiveUntil));
        PolicyVersion version = FindVersion(versionId);
        version.EnsurePublishable(utcEffectiveFrom, utcEffectiveUntil, utcOccurredAt);
        EnsureNoOverlap(versionId, utcEffectiveFrom, utcEffectiveUntil);
        version.Publish(utcEffectiveFrom, utcEffectiveUntil, utcOccurredAt);
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PolicyVersionPublishedDomainEvent(
            Id,
            version.Id,
            version.Number,
            version.Checksum!,
            utcEffectiveFrom,
            utcEffectiveUntil,
            utcOccurredAt));
        return version;
    }

    public PolicyVersion Retire(
        Guid versionId,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = Mutation(
            expectedToken,
            nextToken,
            occurredAt);
        PolicyVersion version = FindVersion(versionId);
        version.Retire(utcOccurredAt);
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PolicyVersionRetiredDomainEvent(
            Id,
            version.Id,
            version.Number,
            version.EffectiveFrom!.Value,
            version.EffectiveUntil,
            utcOccurredAt));
        return version;
    }

    public PolicyVersion GetVersion(Guid versionId)
    {
        return FindVersion(versionId);
    }

    private static PolicyVersionDraftCreatedDomainEvent DraftCreated(
        Policy policy,
        PolicyVersion version,
        DateTimeOffset occurredAt)
    {
        return new PolicyVersionDraftCreatedDomainEvent(
            policy.Id,
            version.Id,
            version.Number,
            version.IsValid,
            version.Checksum,
            occurredAt);
    }

    private DateTimeOffset Mutation(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        return PolicyLifecycleGuard.Mutation(
            ConcurrencyToken,
            expectedToken,
            nextToken,
            LastModifiedAt,
            occurredAt);
    }

    private PolicyVersion FindVersion(Guid versionId)
    {
        return _versions.SingleOrDefault(version => version.Id == versionId)
            ?? throw new DomainRuleException(
                DomainErrorCodes.EntityNotFound,
                "The policy version was not found.",
                nameof(versionId));
    }

    private void EnsureNoOverlap(
        Guid versionId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil)
    {
        bool overlaps = _versions
            .Where(version => version.Id != versionId)
            .Where(version => version.Status != PolicyStatus.Draft)
            .Any(version => RangesOverlap(
                effectiveFrom,
                effectiveUntil,
                version.EffectiveFrom!.Value,
                version.EffectiveUntil));
        if (overlaps)
        {
            throw new DomainRuleException(
                PolicyLifecycleErrorCodes.EffectiveRangeOverlap,
                "Policy version effective ranges cannot overlap.");
        }
    }

    private static bool RangesOverlap(
        DateTimeOffset leftStart,
        DateTimeOffset? leftEnd,
        DateTimeOffset rightStart,
        DateTimeOffset? rightEnd)
    {
        return (rightEnd is null || leftStart < rightEnd)
            && (leftEnd is null || rightStart < leftEnd);
    }

    private void CompleteMutation(
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ConcurrencyToken = nextToken;
        LastModifiedAt = occurredAt;
    }
}
