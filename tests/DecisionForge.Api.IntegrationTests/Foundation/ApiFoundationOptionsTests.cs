using DecisionForge.Api.Foundation;

namespace DecisionForge.Api.IntegrationTests.Foundation;

public sealed class ApiFoundationOptionsTests
{
    [Theory]
    [InlineData(1023, false)]
    [InlineData(1024, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    public void BodyLimitUsesControlledInclusiveBounds(long maximumBytes, bool expected)
    {
        ApiFoundationOptions options = new()
        {
            MaximumRequestBodyBytes = maximumBytes,
        };

        Assert.Equal(expected, options.IsValid());
    }

    [Theory]
    [MemberData(nameof(CorsOriginCases))]
    public void CorsOriginsMustBeUniqueExactAndSecureOrLoopback(
        string[] origins,
        bool expected)
    {
        ApiFoundationOptions options = new()
        {
            AllowedCorsOrigins = origins,
        };

        Assert.Equal(expected, options.IsValid());
    }

    [Fact]
    public void ListDefinitionRejectsAmbiguousOrUncontrolledAllowLists()
    {
        Assert.Throws<ArgumentException>(() => new ApiListQueryDefinition(
            ["name", "NAME"],
            [],
            "name"));
        Assert.Throws<ArgumentException>(() => new ApiListQueryDefinition(
            ["name"],
            ["pageSize"],
            "name"));
        Assert.Throws<ArgumentException>(() => new ApiListQueryDefinition(
            ["invalid-field"],
            [],
            "invalid-field"));
        Assert.Throws<ArgumentException>(() => new ApiListQueryDefinition(
            ["name"],
            [],
            "missing"));
        Assert.Throws<ArgumentException>(() => new ApiListQueryDefinition(
            ["name"],
            [],
            "name",
            (ApiSortDirection)99));
    }

    public static TheoryData<string[], bool> CorsOriginCases()
    {
        return new TheoryData<string[], bool>
        {
            { [], true },
            { ["https://ui.decisionforge.example"], true },
            { ["http://localhost:5173"], true },
            { ["http://127.0.0.1:5173"], true },
            { ["http://ui.decisionforge.example"], false },
            { ["https://ui.decisionforge.example/path"], false },
            { ["https://ui.decisionforge.example?query=1"], false },
            { ["https://ui.decisionforge.example#fragment"], false },
            { ["relative-origin"], false },
            {
                ["https://ui.decisionforge.example", "HTTPS://UI.DECISIONFORGE.EXAMPLE"],
                false
            },
            {
                Enumerable.Range(1, 11)
                    .Select(index => $"https://ui{index}.decisionforge.example")
                    .ToArray(),
                false
            },
        };
    }
}
