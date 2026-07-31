using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.ValueObjects;

public sealed class IdentifierValueObjectTests
{
    [Fact]
    public void ControlledCodesNormalizeAndUseValueEquality()
    {
        RequestNumber requestNumber = RequestNumber.Parse(" pr-2026-000001 ");
        ReasonCode reasonCode = ReasonCode.Parse(" finance_required ");
        DepartmentCode departmentCode = DepartmentCode.Parse(" eng ");
        SupplierRegistrationNumber supplierNumber =
            SupplierRegistrationNumber.Parse(" in/ka-123 ");

        Assert.Equal("PR-2026-000001", requestNumber.Value);
        Assert.Equal(requestNumber, RequestNumber.Parse("PR-2026-000001"));
        Assert.Equal("FINANCE_REQUIRED", reasonCode.ToString());
        Assert.Equal("ENG", departmentCode.ToString());
        Assert.Equal("IN/KA-123", supplierNumber.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad value")]
    [InlineData("bad.value")]
    public void RequestNumberRejectsInvalidValues(string? value)
    {
        AssertValidation(() => RequestNumber.Parse(value!));
    }

    [Fact]
    public void ControlledCodesEnforceMaximumLengthsAndCharacters()
    {
        AssertValidation(() => ReasonCode.Parse(new string('A', 65)));
        AssertValidation(() => DepartmentCode.Parse("ENG!"));
        AssertValidation(() => SupplierRegistrationNumber.Parse("REGISTRATION#1"));
    }

    [Fact]
    public void PolicyVersionIsPositiveMonotonicAndInvariant()
    {
        PolicyVersionNumber first = PolicyVersionNumber.Create(1);

        Assert.Equal(1, first.Value);
        Assert.Equal("1", first.ToString());
        Assert.Equal(2, first.Next().Value);
        AssertValidation(() => PolicyVersionNumber.Create(0));

        DomainRuleException overflow = Assert.Throws<DomainRuleException>(
            () => PolicyVersionNumber.Create(int.MaxValue).Next());
        Assert.Equal(DomainErrorCodes.AmountOverflow, overflow.Code);
    }

    [Fact]
    public void HashValuesNormalizeValidSha256Hex()
    {
        string uppercaseHash = new('A', 64);

        PolicyChecksum checksum = PolicyChecksum.Parse(uppercaseHash);
        AuditHash auditHash = AuditHash.Parse(uppercaseHash);

        Assert.Equal(new string('a', 64), checksum.Value);
        Assert.Equal(checksum.Value, checksum.ToString());
        Assert.Equal(checksum.Value, auditHash.Value);
        Assert.Equal(auditHash.Value, auditHash.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void HashValuesRejectInvalidLength(string? value)
    {
        AssertValidation(() => PolicyChecksum.Parse(value!));
        AssertValidation(() => AuditHash.Parse(value!));
    }

    [Fact]
    public void HashValuesRejectNonHexCharacter()
    {
        AssertValidation(() => PolicyChecksum.Parse(new string('g', 64)));
    }

    [Fact]
    public void ConcurrencyTokenRequiresNonEmptyGuid()
    {
        Guid value = Guid.Parse("50000000-0000-7000-8000-000000000001");

        ConcurrencyToken token = ConcurrencyToken.Create(value);

        Assert.Equal(value, token.Value);
        Assert.Equal(value.ToString("N"), token.ToString());
        AssertValidation(() => ConcurrencyToken.Create(Guid.Empty));
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
