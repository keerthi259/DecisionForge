using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class EffectivePolicySelectorTests
{
    private static readonly DateTimeOffset _submittedAt =
        PurchaseRequestBuilder.DefaultTime;

    [Fact]
    public void SelectsTheOnlyPublishedVersionAtHalfOpenBoundary()
    {
        PolicyEvaluationSource expired = DecisionTestData.PolicySource(
            effectiveFrom: _submittedAt.AddDays(-2),
            effectiveUntil: _submittedAt,
            versionId: Guid.Parse("88888888-8888-4888-8888-888888888881"));
        PolicyEvaluationSource applicable = DecisionTestData.PolicySource(
            effectiveFrom: _submittedAt,
            versionId: Guid.Parse("88888888-8888-4888-8888-888888888882"));

        PolicyEvaluationSource selected = EffectivePolicySelector.Select(
            [expired, applicable],
            _submittedAt);

        Assert.Same(applicable, selected);
    }

    [Fact]
    public void RetiredAndFutureVersionsAreNotApplicable()
    {
        PolicyEvaluationSource retired = DecisionTestData.PolicySource(
            PolicyStatus.Retired,
            effectiveFrom: _submittedAt.AddDays(-2),
            effectiveUntil: _submittedAt.AddDays(1));
        PolicyEvaluationSource future = DecisionTestData.PolicySource(
            effectiveFrom: _submittedAt.AddTicks(1),
            versionId: Guid.Parse("88888888-8888-4888-8888-888888888883"));

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => EffectivePolicySelector.Select([retired, future], _submittedAt));

        Assert.Equal(DecisionErrorCodes.NoEffectivePolicy, exception.Code);
    }

    [Fact]
    public void NoApplicableVersionFailsWithStableCode()
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => EffectivePolicySelector.Select([], _submittedAt));

        Assert.Equal(DecisionErrorCodes.NoEffectivePolicy, exception.Code);
    }

    [Fact]
    public void MultipleApplicableVersionsFailInsteadOfChoosingArbitrarily()
    {
        PolicyEvaluationSource first = DecisionTestData.PolicySource();
        PolicyEvaluationSource second = DecisionTestData.PolicySource(
            policyId: Guid.Parse("77777777-7777-4777-8777-777777777778"),
            versionId: Guid.Parse("88888888-8888-4888-8888-888888888889"));

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => EffectivePolicySelector.Select([first, second], _submittedAt));

        Assert.Equal(DecisionErrorCodes.AmbiguousEffectivePolicy, exception.Code);
    }

    [Fact]
    public void NonUtcSelectionTimestampIsRejected()
    {
        Assert.Throws<DomainRuleException>(
            () => EffectivePolicySelector.Select(
                [DecisionTestData.PolicySource()],
                _submittedAt.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void EvaluationSourceRejectsDraftOrChecksumMismatch()
    {
        PolicyEvaluationSource valid = DecisionTestData.PolicySource();

        Assert.Throws<DomainRuleException>(() => PolicyEvaluationSource.Create(
            valid.PolicyId,
            valid.VersionId,
            valid.VersionNumber,
            PolicyStatus.Draft,
            valid.Checksum,
            valid.Definition,
            valid.EffectiveFrom,
            valid.EffectiveUntil));
        DomainRuleException mismatch = Assert.Throws<DomainRuleException>(
            () => PolicyEvaluationSource.Create(
                valid.PolicyId,
                valid.VersionId,
                valid.VersionNumber,
                PolicyStatus.Published,
                PolicyChecksum.Parse(new string('a', 64)),
                valid.Definition,
                valid.EffectiveFrom,
                valid.EffectiveUntil));
        Assert.Equal(DecisionErrorCodes.PolicyEvidenceMismatch, mismatch.Code);
        Assert.NotEqual(
            PolicyChecksum.Parse(new string('a', 64)),
            PolicyCanonicalSerializer.CalculateChecksum(valid.Definition));
    }
}
