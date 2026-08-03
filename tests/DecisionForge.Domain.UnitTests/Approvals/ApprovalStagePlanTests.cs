using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies;

namespace DecisionForge.Domain.UnitTests.Approvals;

public sealed class ApprovalStagePlanTests
{
    [Fact]
    public void BuilderDeduplicatesAndUsesTheCanonicalFiveRoleOrder()
    {
        IReadOnlyList<PolicyApproverRole> plan = ApprovalStagePlanBuilder.Build(
        [
            PolicyApproverRole.SeniorApprover,
            PolicyApproverRole.FinanceApprover,
            PolicyApproverRole.DepartmentApprover,
            PolicyApproverRole.SecurityApprover,
            PolicyApproverRole.ProcurementApprover,
            PolicyApproverRole.FinanceApprover,
        ]);

        Assert.Equal(
        [
            PolicyApproverRole.DepartmentApprover,
            PolicyApproverRole.ProcurementApprover,
            PolicyApproverRole.SecurityApprover,
            PolicyApproverRole.FinanceApprover,
            PolicyApproverRole.SeniorApprover,
        ],
            plan);
        Assert.Throws<NotSupportedException>(
            () => ((IList<PolicyApproverRole>)plan).Clear());
    }

    [Fact]
    public void BuilderRejectsEmptyNullAndUnknownRolePlans()
    {
        DomainRuleException empty = Assert.Throws<DomainRuleException>(
            () => ApprovalStagePlanBuilder.Build([]));
        Assert.Equal(ApprovalErrorCodes.RolesRequired, empty.Code);
        Assert.Throws<ArgumentNullException>(
            () => ApprovalStagePlanBuilder.Build(null!));
        DomainRuleException invalid = Assert.Throws<DomainRuleException>(
            () => ApprovalStagePlanBuilder.Build([(PolicyApproverRole)999]));
        Assert.Equal(DomainErrorCodes.Validation, invalid.Code);
    }
}
