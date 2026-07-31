using DecisionForge.Application.ReferenceData;
using DecisionForge.Domain.Common;

namespace DecisionForge.Application.UnitTests.ReferenceData;

public sealed class ReferenceDataPageTests
{
    [Fact]
    public void CreateNormalizesSearchAndAcceptsBoundaryValues()
    {
        ReferenceDataPage page = ReferenceDataPage.Create(" engineering ", 0, 100);

        Assert.Equal("engineering", page.Search);
        Assert.Equal(0, page.Offset);
        Assert.Equal(100, page.PageSize);
        Assert.Null(ReferenceDataPage.Create("  ", int.MaxValue, 1).Search);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public void CreateRejectsInvalidPagination(int offset, int pageSize)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => ReferenceDataPage.Create(null, offset, pageSize));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }

    [Fact]
    public void CreateRejectsSearchBeyondMaximumLength()
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => ReferenceDataPage.Create(new string('a', 101), 0, 1));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Equal("search", exception.ParameterName);
    }
}
