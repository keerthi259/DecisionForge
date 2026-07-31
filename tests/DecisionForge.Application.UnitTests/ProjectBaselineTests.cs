using DecisionForge.Testing;

namespace DecisionForge.Application.UnitTests;

public sealed class ProjectBaselineTests
{
    [Fact]
    public void ApplicationProjectTargetsNet10AndReferencesOnlyDomain()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        ProjectFileMetadata project = ProjectFileReader.Read(
            root,
            "src/DecisionForge.Application/DecisionForge.Application.csproj");

        Assert.Equal("net10.0", project.TargetFramework);
        Assert.Equal(["DecisionForge.Domain"], project.ProjectReferences);
    }
}
