using DecisionForge.Application.Platform;
using DecisionForge.Infrastructure.Platform;

namespace DecisionForge.Infrastructure.IntegrationTests;

public sealed class PlatformInfrastructureTests
{
    [Fact]
    public void CorrelationScopesRestoreTheirParent()
    {
        CorrelationContextAccessor accessor = new();

        using (accessor.Push("outer"))
        {
            Assert.Equal("outer", accessor.CorrelationId);
            using (accessor.Push("inner"))
            {
                Assert.Equal("inner", accessor.CorrelationId);
            }

            Assert.Equal("outer", accessor.CorrelationId);
        }

        Assert.Null(accessor.CorrelationId);
    }

    [Fact]
    public void SystemIdGeneratorCreatesVersionSevenIds()
    {
        SystemIdGenerator generator = new(new FixedTimeProvider());

        Guid generated = generator.Create();

        Assert.Equal('7', generated.ToString("D")[14]);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
        }
    }
}
