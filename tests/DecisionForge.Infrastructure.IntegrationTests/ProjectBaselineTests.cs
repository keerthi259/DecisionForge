using DecisionForge.Testing;

namespace DecisionForge.Infrastructure.IntegrationTests;

public sealed class ProjectBaselineTests
{
    [Fact]
    public void InfrastructureProjectTargetsNet10AndUsesApprovedReferences()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        ProjectFileMetadata project = ProjectFileReader.Read(
            root,
            "src/DecisionForge.Infrastructure/DecisionForge.Infrastructure.csproj");

        Assert.Equal("net10.0", project.TargetFramework);
        Assert.Equal(
            ["DecisionForge.Application", "DecisionForge.Domain"],
            project.ProjectReferences);
    }
}
