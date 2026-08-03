using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.Policies.Lifecycle.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Policies.Lifecycle;

public sealed class PolicyLifecycleMutationTests
{
    [Fact]
    public void PublishedVersionAllowsNextMonotonicDraftOnly()
    {
        Policy policy = PublishedPolicy();
        policy.ClearDomainEvents();

        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(2));

        Assert.Equal(2, second.Number.Value);
        Assert.Equal(PolicyStatus.Draft, second.Status);
        Assert.Equal(PolicyLifecycleTestData.ThirdToken, policy.ConcurrencyToken);
        PolicyVersionDraftCreatedDomainEvent created =
            Assert.IsType<PolicyVersionDraftCreatedDomainEvent>(
                Assert.Single(policy.DomainEvents));
        Assert.Equal(2, created.VersionNumber.Value);
        DomainRuleException duplicateDraft = Assert.Throws<DomainRuleException>(() =>
            policy.CreateDraft(
                Guid.Parse("88888888-8888-4888-8888-888888888888"),
                PolicyLifecycleTestData.Definition(),
                PolicyLifecycleTestData.ThirdToken,
                PolicyLifecycleTestData.FourthToken,
                PolicyLifecycleTestData.CreatedAt.AddMinutes(3)));
        Assert.Equal(DomainErrorCodes.InvalidState, duplicateDraft.Code);
    }

    [Fact]
    public void DraftUpdateRefreshesValidationAndChecksumAtomically()
    {
        Policy policy = PolicyLifecycleTestData.Create("{ invalid");
        PolicyVersion version = Assert.Single(policy.Versions);
        policy.ClearDomainEvents();

        policy.UpdateDraft(
            version.Id,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        Assert.True(version.IsValid);
        Assert.NotNull(version.Checksum);
        Assert.Empty(version.ValidationErrors);
        Assert.Equal(PolicyLifecycleTestData.SecondToken, policy.ConcurrencyToken);
        PolicyVersionDraftUpdatedDomainEvent updated =
            Assert.IsType<PolicyVersionDraftUpdatedDomainEvent>(
                Assert.Single(policy.DomainEvents));
        Assert.True(updated.IsValid);
        Assert.Equal(version.Checksum, updated.Checksum);
    }

    [Fact]
    public void IdenticalDraftTextIsANoOp()
    {
        string json = PolicyLifecycleTestData.Definition();
        Policy policy = PolicyLifecycleTestData.Create(json);
        PolicyVersion version = Assert.Single(policy.Versions);
        policy.ClearDomainEvents();

        PolicyVersion result = policy.UpdateDraft(
            version.Id,
            json,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        Assert.Same(version, result);
        Assert.Equal(PolicyLifecycleTestData.InitialToken, policy.ConcurrencyToken);
        Assert.Equal(PolicyLifecycleTestData.CreatedAt, policy.LastModifiedAt);
        Assert.Empty(policy.DomainEvents);
    }

    [Fact]
    public void PublishedAndRetiredVersionsRejectEveryMutationAttempt()
    {
        Policy policy = PublishedPolicy();
        PolicyVersion version = Assert.Single(policy.Versions);

        DomainRuleException updatePublished = Assert.Throws<DomainRuleException>(() =>
            policy.UpdateDraft(
                version.Id,
                PolicyLifecycleTestData.Definition(),
                PolicyLifecycleTestData.SecondToken,
                PolicyLifecycleTestData.ThirdToken,
                PolicyLifecycleTestData.CreatedAt.AddMinutes(2)));
        DomainRuleException publishAgain = Assert.Throws<DomainRuleException>(() =>
            policy.Publish(
                version.Id,
                PolicyLifecycleTestData.CreatedAt.AddHours(2),
                null,
                PolicyLifecycleTestData.SecondToken,
                PolicyLifecycleTestData.ThirdToken,
                PolicyLifecycleTestData.CreatedAt.AddMinutes(2)));
        Assert.Equal(PolicyLifecycleErrorCodes.ImmutableVersion, updatePublished.Code);
        Assert.Equal(PolicyLifecycleErrorCodes.ImmutableVersion, publishAgain.Code);

        policy.Retire(
            version.Id,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(2));
        DomainRuleException updateRetired = Assert.Throws<DomainRuleException>(() =>
            policy.UpdateDraft(
                version.Id,
                PolicyLifecycleTestData.Definition(),
                PolicyLifecycleTestData.ThirdToken,
                PolicyLifecycleTestData.FourthToken,
                PolicyLifecycleTestData.CreatedAt.AddMinutes(3)));
        DomainRuleException retireAgain = Assert.Throws<DomainRuleException>(() =>
            policy.Retire(
                version.Id,
                PolicyLifecycleTestData.ThirdToken,
                PolicyLifecycleTestData.FourthToken,
                PolicyLifecycleTestData.CreatedAt.AddMinutes(3)));
        Assert.Equal(PolicyLifecycleErrorCodes.ImmutableVersion, updateRetired.Code);
        Assert.Equal(DomainErrorCodes.InvalidState, retireAgain.Code);
    }

    [Fact]
    public void StaleAndInvalidConcurrencyInputsLeaveDraftUnchanged()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        PolicyVersion version = Assert.Single(policy.Versions);
        string original = version.DefinitionJson;
        ConcurrencyToken stale = ConcurrencyToken.Create(
            Guid.Parse("89999999-9999-4999-8999-999999999999"));

        DomainRuleException conflict = Assert.Throws<DomainRuleException>(() =>
            policy.UpdateDraft(
                version.Id,
                "{ invalid",
                stale,
                PolicyLifecycleTestData.SecondToken,
                PolicyLifecycleTestData.CreatedAt.AddMinutes(1)));
        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, conflict.Code);
        Assert.Throws<DomainRuleException>(() => policy.UpdateDraft(
            version.Id,
            "{ invalid",
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1)));
        Assert.Throws<DomainRuleException>(() => policy.UpdateDraft(
            version.Id,
            "{ invalid",
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt.AddTicks(-1)));
        Assert.Equal(original, version.DefinitionJson);
        Assert.Equal(PolicyLifecycleTestData.InitialToken, policy.ConcurrencyToken);
    }

    [Fact]
    public void MissingAndDuplicateVersionIdentifiersFailSafely()
    {
        Policy policy = PublishedPolicy();

        DomainRuleException missing = Assert.Throws<DomainRuleException>(
            () => policy.GetVersion(Guid.Parse("8aaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")));
        DomainRuleException duplicate = Assert.Throws<DomainRuleException>(() => policy.CreateDraft(
            PolicyLifecycleTestData.VersionOneId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(2)));
        Assert.Equal(DomainErrorCodes.EntityNotFound, missing.Code);
        Assert.Equal(DomainErrorCodes.DuplicateEntity, duplicate.Code);
    }

    private static Policy PublishedPolicy()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1),
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        return policy;
    }
}
