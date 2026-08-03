using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.UnitTests.Audit;

public sealed class AuditPayloadTests
{
    [Fact]
    public void CanonicalPayloadSortsAndEscapesFieldsDeterministically()
    {
        AuditPayload payload = AuditPayload.Create(
        [
            new("z", "line\nvalue"),
            new("a", "<safe>"),
        ]);

        Assert.Equal(
            """{"a":"\u003Csafe\u003E","z":"line\nvalue"}""",
            payload.CanonicalJson);
        Assert.Equal(["a", "z"], payload.Fields.Keys);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)payload.Fields).Clear());
    }

    [Theory]
    [InlineData("password")]
    [InlineData("authorizationHeader")]
    [InlineData("rejectionReason")]
    [InlineData("policy_json")]
    [InlineData("concurrencyToken")]
    public void SensitiveFieldNamesAreRejected(string name)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => AuditPayload.Create([new(name, "not persisted")]));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }

    [Fact]
    public void DuplicateInvalidAndOversizedFieldsAreRejected()
    {
        Assert.Throws<DomainRuleException>(() => AuditPayload.Create(
            [new("same", "1"), new("same", "2")]));
        Assert.Throws<DomainRuleException>(() => AuditPayload.Create([new("1bad", "value")]));
        Assert.Throws<DomainRuleException>(() => AuditPayload.Create(
            [new("valid", new string('x', AuditPayload.MaximumFieldValueLength + 1))]));
        Assert.Throws<ArgumentNullException>(() => AuditPayload.Create(null!));
    }
}
