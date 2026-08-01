using System.Globalization;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyGoldenEvaluationTests
{
    [Fact]
    public void LowValueApprovedSupplierUsesDefaultAutoApproval()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts());

        Assert.Equal(DecisionDisposition.AutoApproved, result.Disposition);
        Assert.True(result.DefaultOutcomeApplied);
        Assert.Empty(result.RequiredApproverRoles);
        Assert.Equal(["STANDARD_REQUEST"], result.Reasons.Select(reason => reason.Code.Value));
        Assert.All(result.Rules, rule => Assert.False(rule.Matched));
    }

    [Fact]
    public void FlagshipLaptopScenarioProducesFourOrderedApprovalsAndGoldenTrace()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts(
                totalAmount: 2_400_000m,
                onboardingStatus: "InProgress",
                riskRating: "Medium",
                supplierApproved: false,
                containsTechnology: true,
                urgency: "Urgent"));

        Assert.Equal(DecisionDisposition.ManualApprovalRequired, result.Disposition);
        Assert.False(result.DefaultOutcomeApplied);
        Assert.Equal(
            [
                PolicyApproverRole.ProcurementApprover,
                PolicyApproverRole.SecurityApprover,
                PolicyApproverRole.FinanceApprover,
                PolicyApproverRole.SeniorApprover,
            ],
            result.RequiredApproverRoles);
        Assert.Equal(
            ["PROCUREMENT_REVIEW", "SECURITY_REVIEW", "FINANCE_REVIEW", "SENIOR_REVIEW"],
            result.Reasons.Select(reason => reason.Code.Value));
        Assert.Equal(9, result.Rules.Count);
        Assert.Equal(4, result.Rules.Count(rule => rule.Matched));
        Assert.Equal(
            "8a0d654f3ea8cb6c22f5fece9f2a37587751ceac56780b7f6d9396a7a3a62fad",
            result.InputChecksum.Value);
        Assert.Equal(
            "4a9ed5a3cb090268efa434b1332ef652bc455363dba51b81693d10bf0abafeb3",
            result.TraceChecksum.Value);
    }

    [Fact]
    public void SuspendedSupplierRejectionDominatesManualMatches()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts(
                onboardingStatus: "Suspended",
                supplierApproved: false));

        Assert.Equal(DecisionDisposition.Rejected, result.Disposition);
        Assert.Contains(result.Reasons, reason => reason.Code.Value == "SUPPLIER_SUSPENDED");
        Assert.Contains(result.Reasons, reason => reason.Code.Value == "PROCUREMENT_REVIEW");
    }

    [Fact]
    public void RestrictedCloudPurchaseDeduplicatesSecurityRoutingAndReason()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts(
                containsTechnology: true,
                dataSensitivity: "Restricted"));

        Assert.Equal(DecisionDisposition.ManualApprovalRequired, result.Disposition);
        Assert.Equal([PolicyApproverRole.SecurityApprover], result.RequiredApproverRoles);
        Assert.Equal(["SECURITY_REVIEW"], result.Reasons.Select(reason => reason.Code.Value));
        Assert.Equal(2, result.Rules.Count(rule => rule.Matched));
    }

    [Fact]
    public void ExactFinanceThresholdDoesNotMatchGreaterThanRule()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts(totalAmount: 500_000m));

        Assert.Equal(DecisionDisposition.AutoApproved, result.Disposition);
        Assert.False(result.Rules.Single(rule => rule.RuleId == "FINANCE-REVIEW").Matched);
    }

    [Fact]
    public void EmergencyRequestRequiresDepartmentApproval()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts(urgency: "Emergency"));

        Assert.Equal(DecisionDisposition.ManualApprovalRequired, result.Disposition);
        Assert.Equal([PolicyApproverRole.DepartmentApprover], result.RequiredApproverRoles);
        Assert.Equal(["EMERGENCY_EXCEPTION"], result.Reasons.Select(reason => reason.Code.Value));
    }

    [Fact]
    public void MissingJustificationProducesControlledRejection()
    {
        PolicyEvaluationResult result = Evaluate(
            PolicyEvaluationTestData.Facts(hasJustification: false));

        Assert.Equal(DecisionDisposition.Rejected, result.Disposition);
        Assert.Equal(["JUSTIFICATION_REQUIRED"], result.Reasons.Select(reason => reason.Code.Value));
    }

    [Fact]
    public void EvaluationIsCultureIndependentAndResultCollectionsAreImmutable()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            PolicyEvaluationResult invariant = Evaluate(
                PolicyEvaluationTestData.Facts(totalAmount: 500_000.25m));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            PolicyEvaluationResult french = Evaluate(
                PolicyEvaluationTestData.Facts(totalAmount: 500_000.25m));

            Assert.Equal(invariant.InputChecksum, french.InputChecksum);
            Assert.Equal(invariant.TraceChecksum, french.TraceChecksum);
            ICollection<PolicyRuleEvaluation> rules =
                Assert.IsAssignableFrom<ICollection<PolicyRuleEvaluation>>(french.Rules);
            ICollection<PolicyEvaluationReason> reasons =
                Assert.IsAssignableFrom<ICollection<PolicyEvaluationReason>>(french.Reasons);
            ICollection<PolicyApproverRole> roles =
                Assert.IsAssignableFrom<ICollection<PolicyApproverRole>>(
                    french.RequiredApproverRoles);
            Assert.Throws<NotSupportedException>(rules.Clear);
            Assert.Throws<NotSupportedException>(reasons.Clear);
            Assert.Throws<NotSupportedException>(roles.Clear);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static PolicyEvaluationResult Evaluate(PolicyFactSet facts)
    {
        return PolicyEvaluator.Evaluate(PolicyEvaluationTestData.GoldenPolicy(), facts);
    }
}
