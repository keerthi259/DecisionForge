using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Policies;

public sealed record CreatePolicyCommand(
    PolicyCode Code,
    string Name,
    string DefinitionJson);

public sealed record CreateDraftPolicyVersionCommand(
    Guid PolicyId,
    string DefinitionJson,
    ConcurrencyToken ExpectedToken);

public sealed record UpdateDraftPolicyVersionCommand(
    Guid PolicyId,
    Guid PolicyVersionId,
    string DefinitionJson,
    ConcurrencyToken ExpectedToken);

public sealed record ValidateDraftPolicyVersionQuery(
    Guid PolicyId,
    Guid PolicyVersionId);

public sealed record PublishPolicyVersionCommand(
    Guid PolicyId,
    Guid PolicyVersionId,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    ConcurrencyToken ExpectedToken);

public sealed record RetirePolicyVersionCommand(
    Guid PolicyId,
    Guid PolicyVersionId,
    ConcurrencyToken ExpectedToken);

public sealed record ComparePolicyVersionsQuery(
    Guid PolicyId,
    Guid FromPolicyVersionId,
    Guid ToPolicyVersionId);
