using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.ValueObjects;

public sealed class TextValueObjectTests
{
    [Fact]
    public void BusinessJustificationTrimsAndUsesValueEquality()
    {
        BusinessJustification justification = BusinessJustification.Parse("  Required for delivery.  ");

        Assert.Equal("Required for delivery.", justification.Value);
        Assert.Equal(justification, BusinessJustification.Parse("Required for delivery."));
        Assert.Equal("Required for delivery.", justification.ToString());
    }

    [Fact]
    public void BusinessJustificationEnforcesBounds()
    {
        Assert.Equal(
            BusinessJustification.MaximumLength,
            BusinessJustification.Parse(new string('x', BusinessJustification.MaximumLength)).Value.Length);
        AssertValidation(() => BusinessJustification.Parse(null!));
        AssertValidation(() => BusinessJustification.Parse(" "));
        AssertValidation(
            () => BusinessJustification.Parse(
                new string('x', BusinessJustification.MaximumLength + 1)));
    }

    [Theory]
    [InlineData("request-123")]
    [InlineData("ABC_def.456")]
    public void CorrelationIdAcceptsSafeHeaderCharacters(string value)
    {
        Assert.Equal(value, CorrelationId.Parse(value).ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains/slash")]
    public void CorrelationIdRejectsUnsafeValues(string? value)
    {
        AssertValidation(() => CorrelationId.Parse(value!));
    }

    [Fact]
    public void CorrelationIdEnforcesMaximumLength()
    {
        Assert.Equal(
            CorrelationId.MaximumLength,
            CorrelationId.Parse(new string('a', CorrelationId.MaximumLength)).Value.Length);
        AssertValidation(() => CorrelationId.Parse(new string('a', CorrelationId.MaximumLength + 1)));
    }

    [Theory]
    [InlineData("operation-123")]
    [InlineData("A_B.C:4")]
    public void IdempotencyKeyAcceptsVisibleAscii(string value)
    {
        Assert.Equal(value, IdempotencyKey.Parse(value).ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("line\nbreak")]
    public void IdempotencyKeyRejectsUnsafeValues(string? value)
    {
        AssertValidation(() => IdempotencyKey.Parse(value!));
    }

    [Fact]
    public void IdempotencyKeyEnforcesMaximumLength()
    {
        Assert.Equal(
            IdempotencyKey.MaximumLength,
            IdempotencyKey.Parse(new string('a', IdempotencyKey.MaximumLength)).Value.Length);
        AssertValidation(() => IdempotencyKey.Parse(new string('a', IdempotencyKey.MaximumLength + 1)));
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
