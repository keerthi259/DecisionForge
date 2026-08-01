using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;

namespace DecisionForge.Domain.UnitTests.Enums;

public sealed class ControlledEnumParserTests
{
    [Fact]
    public void EveryControlledEnumNameParsesExactly()
    {
        AssertEveryNameParses<PurchaseRequestStatus>();
        AssertEveryNameParses<PolicyStatus>();
        AssertEveryNameParses<DecisionDisposition>();
        AssertEveryNameParses<ApprovalStageStatus>();
        AssertEveryNameParses<ProcurementCategory>();
        AssertEveryNameParses<Urgency>();
        AssertEveryNameParses<DataSensitivity>();
        AssertEveryNameParses<SupplierApprovalStatus>();
        AssertEveryNameParses<SupplierOnboardingStatus>();
        AssertEveryNameParses<SupplierRiskRating>();
        AssertEveryNameParses<PolicyApproverRole>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("draft")]
    [InlineData("0")]
    [InlineData("Unknown")]
    public void TryParseRejectsUncontrolledValues(string? value)
    {
        bool parsed = ControlledEnumParser.TryParse(value, out PurchaseRequestStatus result);

        Assert.False(parsed);
        Assert.Equal(default, result);
    }

    [Fact]
    public void ParseReturnsStableValidationFailure()
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => ControlledEnumParser.Parse<Urgency>("urgent", "urgency"));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Equal("urgency", exception.ParameterName);
    }

    private static void AssertEveryNameParses<TEnum>()
        where TEnum : struct, Enum
    {
        foreach (string name in Enum.GetNames<TEnum>())
        {
            Assert.True(ControlledEnumParser.TryParse(name, out TEnum parsed));
            Assert.Equal(Enum.Parse<TEnum>(name), parsed);
            Assert.Equal(parsed, ControlledEnumParser.Parse<TEnum>(name, "value"));
        }
    }
}
