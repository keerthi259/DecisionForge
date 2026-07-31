using DecisionForge.Testing;

namespace DecisionForge.Domain.UnitTests;

public sealed class ProjectBaselineTests
{
    [Fact]
    public void DomainProjectTargetsNet10AndUsesCentralPackageVersions()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        ProjectFileMetadata project = ProjectFileReader.Read(
            root,
            "src/DecisionForge.Domain/DecisionForge.Domain.csproj");

        Assert.Equal("net10.0", project.TargetFramework);
        Assert.DoesNotContain(project.PackageReferences, reference => reference.SpecifiesVersion);
    }
}
