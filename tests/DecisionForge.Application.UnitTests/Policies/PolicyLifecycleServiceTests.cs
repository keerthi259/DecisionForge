using DecisionForge.Application.Policies;
using DecisionForge.Application.UnitTests.ReferenceData;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Policies;

public sealed class PolicyLifecycleServiceTests
{
    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        RecordingPolicyRepository repository = new();
        RecordingPolicyQueries queries = new();
        SequenceIdGenerator ids = new(PolicyApplicationTestData.PolicyId);
        FixedTimeProvider time = new(PolicyApplicationTestData.Now);

        Assert.Throws<ArgumentNullException>(() =>
            new PolicyLifecycleService(null!, queries, ids, time));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyLifecycleService(repository, null!, ids, time));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyLifecycleService(repository, queries, null!, time));
        Assert.Throws<ArgumentNullException>(() =>
            new PolicyLifecycleService(repository, queries, ids, null!));
    }

    [Fact]
    public async Task CreateChecksUniquenessAndPersistsVersionOne()
    {
        RecordingPolicyRepository repository = new();
        RecordingPolicyQueries queries = new();
        SequenceIdGenerator ids = new(
            PolicyApplicationTestData.PolicyId,
            PolicyApplicationTestData.VersionOneId,
            PolicyApplicationTestData.InitialTokenId);
        PolicyLifecycleService service = Service(repository, queries, ids);
        using CancellationTokenSource source = new();

        Policy policy = await service.CreateAsync(
            new CreatePolicyCommand(
                PolicyCode.Parse("PROCUREMENT-GLOBAL"),
                "Global Procurement Policy",
                PolicyApplicationTestData.ValidJson),
            source.Token);

        Assert.Same(policy, repository.Added);
        Assert.Equal(1, Assert.Single(policy.Versions).Number.Value);
        Assert.Equal(1, queries.Calls);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(source.Token, repository.LastCancellationToken);
        Assert.Equal(3, ids.Calls);
    }

    [Fact]
    public async Task DuplicateCodeDoesNotGenerateOrPersist()
    {
        RecordingPolicyRepository repository = new();
        RecordingPolicyQueries queries = new() { CodeExists = true };
        SequenceIdGenerator ids = new(PolicyApplicationTestData.PolicyId);
        PolicyLifecycleService service = Service(repository, queries, ids);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.CreateAsync(
                new CreatePolicyCommand(
                    PolicyCode.Parse("PROCUREMENT-GLOBAL"),
                    "Global Procurement Policy",
                    PolicyApplicationTestData.ValidJson),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.DuplicateEntity, exception.Code);
        Assert.Null(repository.Added);
        Assert.Equal(0, repository.SaveCalls);
        Assert.Equal(0, ids.Calls);
    }

    [Fact]
    public async Task CreateDraftAllocatesNextVersionAndPropagatesCancellation()
    {
        Policy existing = Published();
        RecordingPolicyRepository repository = new() { Existing = existing };
        SequenceIdGenerator ids = new(
            PolicyApplicationTestData.VersionTwoId,
            PolicyApplicationTestData.ThirdTokenId);
        PolicyLifecycleService service = Service(repository, new RecordingPolicyQueries(), ids);
        using CancellationTokenSource source = new();

        PolicyVersion result = await service.CreateDraftAsync(
            new CreateDraftPolicyVersionCommand(
                existing.Id,
                PolicyApplicationTestData.ValidJson,
                existing.ConcurrencyToken),
            source.Token);

        Assert.Equal(2, result.Number.Value);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(source.Token, repository.LastCancellationToken);
    }

    [Fact]
    public async Task UpdateAndValidationPreserveInvalidDraftAndSafeErrors()
    {
        Policy existing = PolicyApplicationTestData.Existing();
        RecordingPolicyRepository repository = new() { Existing = existing };
        PolicyLifecycleService service = Service(
            repository,
            new RecordingPolicyQueries(),
            new SequenceIdGenerator(PolicyApplicationTestData.NextTokenId));

        PolicyVersion updated = await service.UpdateDraftAsync(
            new UpdateDraftPolicyVersionCommand(
                existing.Id,
                PolicyApplicationTestData.VersionOneId,
                "{ malformed",
                existing.ConcurrencyToken),
            CancellationToken.None);
        PolicyDraftValidationResult validation = await service.ValidateDraftAsync(
            new ValidateDraftPolicyVersionQuery(existing.Id, updated.Id),
            CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Null(validation.Checksum);
        Assert.Equal("policy.json.malformed", Assert.Single(validation.Errors).Code);
        Assert.Equal(1, repository.SaveCalls);
        ICollection<DecisionForge.Domain.Policies.Validation.PolicyValidationError> errors =
            Assert.IsAssignableFrom<ICollection<DecisionForge.Domain.Policies.Validation.PolicyValidationError>>(
                validation.Errors);
        Assert.Throws<NotSupportedException>(errors.Clear);
    }

    [Fact]
    public async Task IdenticalDraftDoesNotSave()
    {
        Policy existing = PolicyApplicationTestData.Existing();
        RecordingPolicyRepository repository = new() { Existing = existing };
        PolicyLifecycleService service = Service(
            repository,
            new RecordingPolicyQueries(),
            new SequenceIdGenerator(PolicyApplicationTestData.NextTokenId));

        await service.UpdateDraftAsync(
            new UpdateDraftPolicyVersionCommand(
                existing.Id,
                PolicyApplicationTestData.VersionOneId,
                PolicyApplicationTestData.ValidJson,
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task PublishAndRetirePersistControlledTransitions()
    {
        Policy existing = PolicyApplicationTestData.Existing();
        RecordingPolicyRepository repository = new() { Existing = existing };
        PolicyLifecycleService publishService = Service(
            repository,
            new RecordingPolicyQueries(),
            new SequenceIdGenerator(PolicyApplicationTestData.NextTokenId));
        DateTimeOffset effectiveFrom = PolicyApplicationTestData.Now.AddMinutes(1);

        PolicyVersion published = await publishService.PublishAsync(
            new PublishPolicyVersionCommand(
                existing.Id,
                PolicyApplicationTestData.VersionOneId,
                effectiveFrom,
                null,
                existing.ConcurrencyToken),
            CancellationToken.None);
        PolicyLifecycleService retireService = Service(
            repository,
            new RecordingPolicyQueries(),
            new SequenceIdGenerator(PolicyApplicationTestData.ThirdTokenId),
            effectiveFrom.AddMinutes(1));
        PolicyVersion retired = await retireService.RetireAsync(
            new RetirePolicyVersionCommand(
                existing.Id,
                published.Id,
                existing.ConcurrencyToken),
            CancellationToken.None);

        Assert.Equal(PolicyStatus.Retired, retired.Status);
        Assert.Equal(2, repository.SaveCalls);
    }

    [Fact]
    public async Task InvalidPublishDoesNotSave()
    {
        Policy existing = PolicyApplicationTestData.Existing("{ invalid");
        RecordingPolicyRepository repository = new() { Existing = existing };
        PolicyLifecycleService service = Service(
            repository,
            new RecordingPolicyQueries(),
            new SequenceIdGenerator(PolicyApplicationTestData.NextTokenId));

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.PublishAsync(
                new PublishPolicyVersionCommand(
                    existing.Id,
                    PolicyApplicationTestData.VersionOneId,
                    PolicyApplicationTestData.Now.AddMinutes(1),
                    null,
                    existing.ConcurrencyToken),
                CancellationToken.None));

        Assert.Equal(PolicyLifecycleErrorCodes.InvalidDefinition, exception.Code);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task CompareReturnsStructuredDomainDiff()
    {
        Policy existing = Published();
        PolicyVersion second = existing.CreateDraft(
            PolicyApplicationTestData.VersionTwoId,
            PolicyApplicationTestData.ValidJson.Replace(
                "\"rules\":[]",
                "\"rules\":[{\"id\":\"ADDED\",\"priority\":1,\"when\":{\"fact\":\"supplier.isActive\",\"operator\":\"equals\",\"value\":true},\"then\":{\"disposition\":\"Rejected\",\"reasonCode\":\"ADDED\",\"message\":\"Added.\"}}]",
                StringComparison.Ordinal),
            existing.ConcurrencyToken,
            ConcurrencyToken.Create(PolicyApplicationTestData.ThirdTokenId),
            PolicyApplicationTestData.Now.AddSeconds(1));
        RecordingPolicyRepository repository = new() { Existing = existing };
        PolicyLifecycleService service = Service(
            repository,
            new RecordingPolicyQueries(),
            new SequenceIdGenerator());

        PolicyVersionDiff diff = await service.CompareAsync(
            new ComparePolicyVersionsQuery(
                existing.Id,
                PolicyApplicationTestData.VersionOneId,
                second.Id),
            CancellationToken.None);

        Assert.Equal(["ADDED"], diff.AddedRuleIds);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task MissingPolicyAndPreCancellationFailBeforeMutation()
    {
        RecordingPolicyRepository repository = new();
        RecordingPolicyQueries queries = new();
        PolicyLifecycleService service = Service(
            repository,
            queries,
            new SequenceIdGenerator(PolicyApplicationTestData.NextTokenId));
        DomainRuleException missing = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.ValidateDraftAsync(
                new ValidateDraftPolicyVersionQuery(
                    PolicyApplicationTestData.PolicyId,
                    PolicyApplicationTestData.VersionOneId),
                CancellationToken.None));
        using CancellationTokenSource source = new();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateAsync(
            new CreatePolicyCommand(
                PolicyCode.Parse("PROCUREMENT-GLOBAL"),
                "Global Procurement Policy",
                PolicyApplicationTestData.ValidJson),
            source.Token));

        Assert.Equal(DomainErrorCodes.EntityNotFound, missing.Code);
        Assert.Equal(1, repository.FindCalls);
        Assert.Equal(0, queries.Calls);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public async Task NullCommandsAreRejected()
    {
        PolicyLifecycleService service = Service(
            new RecordingPolicyRepository(),
            new RecordingPolicyQueries(),
            new SequenceIdGenerator());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CreateAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.UpdateDraftAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CompareAsync(null!, CancellationToken.None));
    }

    private static Policy Published()
    {
        Policy policy = PolicyApplicationTestData.Existing();
        policy.Publish(
            PolicyApplicationTestData.VersionOneId,
            PolicyApplicationTestData.Now.AddMinutes(1),
            PolicyApplicationTestData.Now.AddHours(1),
            policy.ConcurrencyToken,
            ConcurrencyToken.Create(PolicyApplicationTestData.NextTokenId),
            PolicyApplicationTestData.Now);
        return policy;
    }

    private static PolicyLifecycleService Service(
        RecordingPolicyRepository repository,
        RecordingPolicyQueries queries,
        SequenceIdGenerator ids,
        DateTimeOffset? now = null)
    {
        return new PolicyLifecycleService(
            repository,
            queries,
            ids,
            new FixedTimeProvider(now ?? PolicyApplicationTestData.Now));
    }
}
