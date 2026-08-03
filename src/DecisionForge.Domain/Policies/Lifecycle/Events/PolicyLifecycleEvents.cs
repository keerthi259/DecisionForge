using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Lifecycle.Events;

public sealed record PolicyCreatedDomainEvent(
    Guid PolicyId,
    PolicyCode Code,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PolicyVersionDraftCreatedDomainEvent(
    Guid PolicyId,
    Guid PolicyVersionId,
    PolicyVersionNumber VersionNumber,
    bool IsValid,
    PolicyChecksum? Checksum,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PolicyVersionDraftUpdatedDomainEvent(
    Guid PolicyId,
    Guid PolicyVersionId,
    PolicyVersionNumber VersionNumber,
    bool IsValid,
    PolicyChecksum? Checksum,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PolicyVersionPublishedDomainEvent(
    Guid PolicyId,
    Guid PolicyVersionId,
    PolicyVersionNumber VersionNumber,
    PolicyChecksum Checksum,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PolicyVersionRetiredDomainEvent(
    Guid PolicyId,
    Guid PolicyVersionId,
    PolicyVersionNumber VersionNumber,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    DateTimeOffset OccurredAt) : IDomainEvent;
