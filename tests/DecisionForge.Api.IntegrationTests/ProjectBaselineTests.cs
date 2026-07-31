using DecisionForge.Testing;

namespace DecisionForge.Api.IntegrationTests;

public sealed class ProjectBaselineTests
{
    [Fact]
    public void ApiProjectTargetsNet10AndUsesApprovedReferences()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        ProjectFileMetadata project = ProjectFileReader.Read(
            root,
            "src/DecisionForge.Api/DecisionForge.Api.csproj");

        Assert.Equal("net10.0", project.TargetFramework);
        Assert.Equal(
            [
                "DecisionForge.Application",
                "DecisionForge.Infrastructure",
                "DecisionForge.ServiceDefaults",
            ],
            project.ProjectReferences);
    }
}
