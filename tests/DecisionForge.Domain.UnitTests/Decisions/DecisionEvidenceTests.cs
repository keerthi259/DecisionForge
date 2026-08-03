using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.UnitTests.Builders;

namespace DecisionForge.Domain.UnitTests.Decisions;

public sealed class DecisionEvidenceTests
{
    [Fact]
    public void CreateCopiesExactPolicyInputOutcomeAndEveryRuleTrace()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        PolicyEvaluationSource source = DecisionTestData.PolicySource();
        PurchaseRequestEvaluationContext context = DecisionTestData.Context(request, source);
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            source.Definition,
            PolicyFactSet.FromSnapshot(context.NormalizedInput));
        Guid ruleId = Guid.Parse("99999999-9999-4999-8999-999999999991");

        Decision decision = Decision.Create(
            Guid.Parse("99999999-9999-4999-8999-999999999990"),
            request.Id,
            source,
            context,
            result,
            [ruleId],
            PurchaseRequestBuilder.DefaultTime);

        Assert.Equal(request.Id, decision.PurchaseRequestId);
        Assert.Equal(source.PolicyId, decision.PolicyId);
        Assert.Equal(source.VersionId, decision.PolicyVersionId);
        Assert.Equal(source.VersionNumber, decision.PolicyVersionNumber);
        Assert.Equal(source.Checksum, decision.PolicyChecksum);
        Assert.Same(context.NormalizedInput, decision.NormalizedInput);
        Assert.Equal(result.Disposition, decision.Disposition);
        Assert.Equal(result.InputChecksum, decision.InputChecksum);
        Assert.Equal(result.TraceChecksum, decision.TraceChecksum);
        RuleEvaluation rule = Assert.Single(decision.Rules);
        Assert.Equal(ruleId, rule.Id);
        Assert.Equal("ACTIVE-SUPPLIER", rule.RuleId);
        Assert.Equal(result.Rules[0].Condition, rule.Condition);
        Assert.Equal(result.Rules[0].Matched, rule.Matched);
        Assert.Single(decision.DomainEvents);
    }

    [Fact]
    public void EvidenceCollectionsCannotBeMutatedAndHaveNoPublicSetters()
    {
        Decision decision = CreateDecision();

        Assert.Throws<NotSupportedException>(
            () => ((IList<RuleEvaluation>)decision.Rules).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<DecisionReason>)decision.Reasons).Clear());
        Assert.Throws<NotSupportedException>(
            () => ((IList<PolicyApproverRole>)decision.RequiredApproverRoles)
                .Clear());
        Assert.DoesNotContain(
            typeof(Decision).GetProperties(),
            property => property.SetMethod?.IsPublic == true);
        Assert.DoesNotContain(
            typeof(RuleEvaluation).GetProperties(),
            property => property.SetMethod?.IsPublic == true);
    }

    [Fact]
    public void RuleIdentityCountAndPolicyEvidenceMustMatch()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        PolicyEvaluationSource source = DecisionTestData.PolicySource();
        PurchaseRequestEvaluationContext context = DecisionTestData.Context(request, source);
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            source.Definition,
            PolicyFactSet.FromSnapshot(context.NormalizedInput));
        PolicyEvaluationSource different = DecisionTestData.PolicySource(
            policyId: Guid.Parse("77777777-7777-4777-8777-777777777779"));

        Assert.Throws<DomainRuleException>(() => Decision.Create(
            Guid.NewGuid(),
            request.Id,
            source,
            context,
            result,
            [],
            PurchaseRequestBuilder.DefaultTime));
        DomainRuleException mismatch = Assert.Throws<DomainRuleException>(() => Decision.Create(
            Guid.NewGuid(),
            request.Id,
            different,
            context,
            result,
            [Guid.NewGuid()],
            PurchaseRequestBuilder.DefaultTime));
        Assert.Equal(DecisionErrorCodes.PolicyEvidenceMismatch, mismatch.Code);
    }

    [Fact]
    public void EquivalentReproductionComparesChecksumsAndControlledOutcome()
    {
        Decision decision = CreateDecision();
        PolicyEvaluationResult exact = PolicyEvaluator.Evaluate(
            DecisionTestData.PolicySource().Definition,
            PolicyFactSet.FromSnapshot(decision.NormalizedInput));

        Assert.True(decision.IsEquivalentTo(exact));
    }

    private static Decision CreateDecision()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        PolicyEvaluationSource source = DecisionTestData.PolicySource();
        PurchaseRequestEvaluationContext context = DecisionTestData.Context(request, source);
        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            source.Definition,
            PolicyFactSet.FromSnapshot(context.NormalizedInput));
        return Decision.Create(
            Guid.Parse("99999999-9999-4999-8999-999999999990"),
            request.Id,
            source,
            context,
            result,
            [Guid.Parse("99999999-9999-4999-8999-999999999991")],
            PurchaseRequestBuilder.DefaultTime);
    }
}
