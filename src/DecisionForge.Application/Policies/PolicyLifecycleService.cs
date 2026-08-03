using DecisionForge.Application.Platform;
using DecisionForge.Application.Policies.Ports;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Policies;

public sealed class PolicyLifecycleService
{
    private readonly IPolicyRepository _repository;
    private readonly IPolicyQueries _queries;
    private readonly IIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public PolicyLifecycleService(
        IPolicyRepository repository,
        IPolicyQueries queries,
        IIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _queries = queries;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<Policy> CreateAsync(
        CreatePolicyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Code);
        cancellationToken.ThrowIfCancellationRequested();
        if (await _queries.CodeExistsAsync(command.Code, cancellationToken))
        {
            throw new DomainRuleException(
                DomainErrorCodes.DuplicateEntity,
                $"Policy code '{command.Code}' already exists.",
                nameof(command.Code));
        }

        Policy policy = Policy.Create(
            _idGenerator.Create(),
            _idGenerator.Create(),
            command.Code,
            command.Name,
            command.DefinitionJson,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.AddAsync(policy, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task<PolicyVersion> CreateDraftAsync(
        CreateDraftPolicyVersionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Policy policy = await FindRequiredAsync(command.PolicyId, cancellationToken);
        PolicyVersion version = policy.CreateDraft(
            _idGenerator.Create(),
            command.DefinitionJson,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<PolicyVersion> UpdateDraftAsync(
        UpdateDraftPolicyVersionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Policy policy = await FindRequiredAsync(command.PolicyId, cancellationToken);
        ConcurrencyToken previousToken = policy.ConcurrencyToken;
        PolicyVersion version = policy.UpdateDraft(
            command.PolicyVersionId,
            command.DefinitionJson,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        if (policy.ConcurrencyToken != previousToken)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }

        return version;
    }

    public async Task<PolicyDraftValidationResult> ValidateDraftAsync(
        ValidateDraftPolicyVersionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Policy policy = await FindRequiredAsync(query.PolicyId, cancellationToken);
        PolicyVersion version = policy.GetVersion(query.PolicyVersionId);
        return new PolicyDraftValidationResult(
            policy.Id,
            version.Id,
            version.Number,
            version.Checksum,
            version.ValidationErrors);
    }

    public async Task<PolicyVersion> PublishAsync(
        PublishPolicyVersionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Policy policy = await FindRequiredAsync(command.PolicyId, cancellationToken);
        PolicyVersion version = policy.Publish(
            command.PolicyVersionId,
            command.EffectiveFrom,
            command.EffectiveUntil,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<PolicyVersion> RetireAsync(
        RetirePolicyVersionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Policy policy = await FindRequiredAsync(command.PolicyId, cancellationToken);
        PolicyVersion version = policy.Retire(
            command.PolicyVersionId,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<PolicyVersionDiff> CompareAsync(
        ComparePolicyVersionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Policy policy = await FindRequiredAsync(query.PolicyId, cancellationToken);
        return PolicyVersionComparer.Compare(
            policy.GetVersion(query.FromPolicyVersionId),
            policy.GetVersion(query.ToPolicyVersionId));
    }

    private async Task<Policy> FindRequiredAsync(
        Guid policyId,
        CancellationToken cancellationToken)
    {
        Policy? policy = await _repository.FindByIdAsync(policyId, cancellationToken);
        return policy
            ?? throw new DomainRuleException(
                DomainErrorCodes.EntityNotFound,
                $"Policy '{policyId}' was not found.",
                nameof(policyId));
    }

    private ConcurrencyToken NextToken()
    {
        return ConcurrencyToken.Create(_idGenerator.Create());
    }
}
