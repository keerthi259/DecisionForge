using DecisionForge.Testing;

namespace DecisionForge.PerformanceTests;

public sealed class ProjectBaselineTests
{
    [Fact]
    public void PerformanceTestProjectUsesCentralPackageVersions()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        ProjectFileMetadata project = ProjectFileReader.Read(
            root,
            "tests/DecisionForge.PerformanceTests/DecisionForge.PerformanceTests.csproj");

        Assert.Equal("net10.0", project.TargetFramework);
        Assert.DoesNotContain(project.PackageReferences, reference => reference.SpecifiesVersion);
    }
}
