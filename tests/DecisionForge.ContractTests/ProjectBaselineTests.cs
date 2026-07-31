using DecisionForge.Testing;

namespace DecisionForge.ContractTests;

public sealed class ProjectBaselineTests
{
    [Fact]
    public void ContractTestProjectUsesCentralPackageVersions()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        ProjectFileMetadata project = ProjectFileReader.Read(
            root,
            "tests/DecisionForge.ContractTests/DecisionForge.ContractTests.csproj");

        Assert.Equal("net10.0", project.TargetFramework);
        Assert.DoesNotContain(project.PackageReferences, reference => reference.SpecifiesVersion);
    }
}
