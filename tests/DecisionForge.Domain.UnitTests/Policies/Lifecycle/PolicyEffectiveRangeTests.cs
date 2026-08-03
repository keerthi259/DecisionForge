using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.Policies.Lifecycle.Events;

namespace DecisionForge.Domain.UnitTests.Policies.Lifecycle;

public sealed class PolicyEffectiveRangeTests
{
    [Fact]
    public void PublishStoresHalfOpenEffectiveRangeAndEvent()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        DateTimeOffset from = PolicyLifecycleTestData.CreatedAt.AddHours(1);
        DateTimeOffset until = from.AddDays(30);
        policy.ClearDomainEvents();

        PolicyVersion version = policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            from,
            until,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);

        Assert.Equal(PolicyStatus.Published, version.Status);
        Assert.Equal(from, version.EffectiveFrom);
        Assert.Equal(until, version.EffectiveUntil);
        Assert.Equal(PolicyLifecycleTestData.CreatedAt, version.PublishedAt);
        PolicyVersionPublishedDomainEvent published =
            Assert.IsType<PolicyVersionPublishedDomainEvent>(
                Assert.Single(policy.DomainEvents));
        Assert.Equal(version.Checksum, published.Checksum);
        Assert.Equal(from, published.EffectiveFrom);
        Assert.Equal(until, published.EffectiveUntil);
    }

    [Fact]
    public void AdjacentEffectiveRangesDoNotOverlap()
    {
        DateTimeOffset firstStart = PolicyLifecycleTestData.CreatedAt.AddHours(10);
        DateTimeOffset boundary = firstStart.AddDays(1);
        Policy policy = PolicyLifecycleTestData.Create();
        policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            firstStart,
            boundary,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        policy.Publish(
            second.Id,
            boundary,
            boundary.AddDays(1),
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.FourthToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(2));

        Assert.Equal(PolicyStatus.Published, second.Status);
        Assert.Equal(boundary, second.EffectiveFrom);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, 2)]
    [InlineData(-1, 3)]
    public void OverlappingRangesAreRejectedWithoutMutation(
        int startHourOffset,
        int endHourOffset)
    {
        DateTimeOffset firstStart = PolicyLifecycleTestData.CreatedAt.AddHours(10);
        DateTimeOffset firstEnd = firstStart.AddHours(2);
        Policy policy = PolicyLifecycleTestData.Create();
        policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            firstStart,
            firstEnd,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => policy.Publish(
            second.Id,
            firstStart.AddHours(startHourOffset),
            firstStart.AddHours(endHourOffset),
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.FourthToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(2)));

        Assert.Equal(PolicyLifecycleErrorCodes.EffectiveRangeOverlap, exception.Code);
        Assert.Equal(PolicyStatus.Draft, second.Status);
        Assert.Equal(PolicyLifecycleTestData.ThirdToken, policy.ConcurrencyToken);
    }

    [Fact]
    public void OpenEndedRangeOverlapsEveryLaterRange()
    {
        DateTimeOffset firstStart = PolicyLifecycleTestData.CreatedAt.AddHours(1);
        Policy policy = PolicyLifecycleTestData.Create();
        policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            firstStart,
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => policy.Publish(
            second.Id,
            firstStart.AddYears(10),
            null,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.FourthToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(2)));

        Assert.Equal(PolicyLifecycleErrorCodes.EffectiveRangeOverlap, exception.Code);
    }

    [Fact]
    public void RetirementClosesRangeAndAllowsReplacementAtExactBoundary()
    {
        DateTimeOffset firstStart = PolicyLifecycleTestData.CreatedAt.AddMinutes(1);
        DateTimeOffset retirement = firstStart.AddHours(1);
        Policy policy = PolicyLifecycleTestData.Create();
        PolicyVersion first = policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            firstStart,
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        PolicyVersion second = policy.CreateDraft(
            PolicyLifecycleTestData.VersionTwoId,
            PolicyLifecycleTestData.Definition(),
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));
        policy.ClearDomainEvents();

        policy.Retire(
            first.Id,
            PolicyLifecycleTestData.ThirdToken,
            PolicyLifecycleTestData.FourthToken,
            retirement);
        PolicyVersionRetiredDomainEvent retired =
            Assert.IsType<PolicyVersionRetiredDomainEvent>(
                Assert.Single(policy.DomainEvents));

        Assert.Equal(PolicyStatus.Retired, first.Status);
        Assert.Equal(retirement, first.EffectiveUntil);
        Assert.Equal(retirement, first.RetiredAt);
        Assert.Equal(retirement, retired.EffectiveUntil);
        policy.Publish(
            second.Id,
            retirement,
            null,
            PolicyLifecycleTestData.FourthToken,
            DecisionForge.Domain.ValueObjects.ConcurrencyToken.Create(
                Guid.Parse("8bbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb")),
            retirement);
        Assert.Equal(retirement, second.EffectiveFrom);
    }

    [Fact]
    public void InvalidDateBoundariesAreRejected()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        DateTimeOffset now = PolicyLifecycleTestData.CreatedAt;

        AssertValidation(() => policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            now.AddTicks(-1),
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            now));
        AssertValidation(() => policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            now.AddHours(1),
            now.AddHours(1),
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            now));
        AssertValidation(() => policy.Publish(
            PolicyLifecycleTestData.VersionOneId,
            now.AddHours(1).ToOffset(TimeSpan.FromHours(1)),
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            now));
        Assert.Equal(PolicyStatus.Draft, Assert.Single(policy.Versions).Status);
    }

    [Fact]
    public void RetirementCannotCreateEmptyRangeOrExtendAnExpiredRange()
    {
        DateTimeOffset start = PolicyLifecycleTestData.CreatedAt.AddMinutes(1);
        DateTimeOffset end = start.AddMinutes(1);
        Policy atBoundary = PolicyLifecycleTestData.Create();
        atBoundary.Publish(
            PolicyLifecycleTestData.VersionOneId,
            start,
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        AssertValidation(() => atBoundary.Retire(
            PolicyLifecycleTestData.VersionOneId,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            start));

        Policy expired = PolicyLifecycleTestData.Create();
        PolicyVersion version = expired.Publish(
            PolicyLifecycleTestData.VersionOneId,
            start,
            end,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt);
        expired.Retire(
            version.Id,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.ThirdToken,
            end.AddMinutes(1));

        Assert.Equal(end, version.EffectiveUntil);
        Assert.Equal(end.AddMinutes(1), version.RetiredAt);
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
