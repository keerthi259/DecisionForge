using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Lifecycle;

public sealed class PolicyVersion : Entity
{
    private ReadOnlyCollection<PolicyValidationError> _validationErrors;

    private PolicyVersion(
        Guid id,
        PolicyVersionNumber number,
        string definitionJson,
        DateTimeOffset createdAt)
        : base(id)
    {
        Number = number;
        DefinitionJson = definitionJson;
        Status = PolicyStatus.Draft;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
        _validationErrors = new List<PolicyValidationError>().AsReadOnly();
    }

    public PolicyVersionNumber Number { get; }

    public PolicyStatus Status { get; private set; }

    public string DefinitionJson { get; private set; }

    public PolicyDefinition? Definition { get; private set; }

    public PolicyChecksum? Checksum { get; private set; }

    public IReadOnlyList<PolicyValidationError> ValidationErrors => _validationErrors;

    public bool IsValid => Definition is not null && _validationErrors.Count == 0;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveUntil { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    internal static PolicyVersion CreateDraft(
        Guid id,
        PolicyVersionNumber number,
        PolicyCode policyCode,
        string policyName,
        string? definitionJson,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(policyCode);
        string storedJson = definitionJson ?? string.Empty;
        PolicyVersion version = new(id, number, storedJson, createdAt);
        version.ApplyDefinition(policyCode, policyName, storedJson);
        return version;
    }

    internal bool UpdateDraft(
        PolicyCode policyCode,
        string policyName,
        string? definitionJson,
        DateTimeOffset occurredAt)
    {
        PolicyLifecycleGuard.Draft(this);
        string storedJson = definitionJson ?? string.Empty;
        if (string.Equals(DefinitionJson, storedJson, StringComparison.Ordinal))
        {
            return false;
        }

        ApplyDefinition(policyCode, policyName, storedJson);
        DefinitionJson = storedJson;
        LastModifiedAt = occurredAt;
        return true;
    }

    internal void Publish(
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil,
        DateTimeOffset occurredAt)
    {
        EnsurePublishable(effectiveFrom, effectiveUntil, occurredAt);

        Status = PolicyStatus.Published;
        PublishedAt = occurredAt;
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
        LastModifiedAt = occurredAt;
    }

    internal void EnsurePublishable(
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil,
        DateTimeOffset occurredAt)
    {
        PolicyLifecycleGuard.Draft(this);
        if (!IsValid)
        {
            throw new DomainRuleException(
                PolicyLifecycleErrorCodes.InvalidDefinition,
                "A policy version must pass validation before publication.");
        }

        if (effectiveFrom < occurredAt)
        {
            throw DomainGuard.Validation(
                nameof(effectiveFrom),
                "A policy version cannot be published with a past effective time.");
        }

        if (effectiveUntil is not null && effectiveUntil <= effectiveFrom)
        {
            throw DomainGuard.Validation(
                nameof(effectiveUntil),
                "A policy effective end must be later than its start.");
        }
    }

    internal void Retire(DateTimeOffset occurredAt)
    {
        if (Status != PolicyStatus.Published)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                "Only a published policy version can be retired.");
        }

        if (occurredAt <= EffectiveFrom)
        {
            throw DomainGuard.Validation(
                nameof(occurredAt),
                "A policy version must have a non-empty effective range before retirement.");
        }

        Status = PolicyStatus.Retired;
        RetiredAt = occurredAt;
        if (EffectiveUntil is null || occurredAt < EffectiveUntil)
        {
            EffectiveUntil = occurredAt;
        }

        LastModifiedAt = occurredAt;
    }

    private void ApplyDefinition(
        PolicyCode policyCode,
        string policyName,
        string definitionJson)
    {
        PolicyParseResult parsed = PolicyJsonParser.Parse(definitionJson);
        List<PolicyValidationError> errors = [.. parsed.Errors];
        PolicyDefinition? definition = parsed.Definition;
        if (definition is not null
            && !string.Equals(definition.PolicyCode, policyCode.Value, StringComparison.Ordinal))
        {
            errors.Add(new PolicyValidationError(
                "$.policyCode",
                "policy.identity.code-mismatch",
                PolicyValidationSeverity.Error,
                "The version policy code does not match its policy."));
        }

        if (definition is not null
            && !string.Equals(definition.Name, policyName, StringComparison.Ordinal))
        {
            errors.Add(new PolicyValidationError(
                "$.name",
                "policy.identity.name-mismatch",
                PolicyValidationSeverity.Error,
                "The version policy name does not match its policy."));
        }

        if (errors.Count == 0 && definition is not null)
        {
            Definition = definition;
            Checksum = PolicyCanonicalSerializer.CalculateChecksum(definition);
        }
        else
        {
            Definition = null;
            Checksum = null;
        }

        _validationErrors = errors.AsReadOnly();
    }
}
