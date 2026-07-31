using DecisionForge.Testing;

namespace DecisionForge.ArchitectureTests;

public sealed class ProjectReferenceTests
{
    private static readonly IReadOnlyDictionary<string, string> _projectPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DecisionForge.Api"] = "src/DecisionForge.Api/DecisionForge.Api.csproj",
            ["DecisionForge.AppHost"] =
                "src/DecisionForge.AppHost/DecisionForge.AppHost.csproj",
            ["DecisionForge.Application"] =
                "src/DecisionForge.Application/DecisionForge.Application.csproj",
            ["DecisionForge.Domain"] = "src/DecisionForge.Domain/DecisionForge.Domain.csproj",
            ["DecisionForge.Infrastructure"] =
                "src/DecisionForge.Infrastructure/DecisionForge.Infrastructure.csproj",
            ["DecisionForge.ServiceDefaults"] =
                "src/DecisionForge.ServiceDefaults/DecisionForge.ServiceDefaults.csproj",
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _allowedReferences =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["DecisionForge.Api"] = Set(
                "DecisionForge.Application",
                "DecisionForge.Infrastructure",
                "DecisionForge.ServiceDefaults"),
            ["DecisionForge.AppHost"] = Set("DecisionForge.Api"),
            ["DecisionForge.Application"] = Set("DecisionForge.Domain"),
            ["DecisionForge.Domain"] = Set(),
            ["DecisionForge.Infrastructure"] =
                Set("DecisionForge.Application", "DecisionForge.Domain"),
            ["DecisionForge.ServiceDefaults"] = Set(),
        };

    [Fact]
    public void ProductionProjectsMatchApprovedDependencyGraph()
    {
        string root = ProjectFileReader.FindRepositoryRoot(AppContext.BaseDirectory);
        Dictionary<string, IReadOnlySet<string>> actual = _projectPaths.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<string>)ProjectFileReader.Read(root, pair.Value)
                .ProjectReferences
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        IReadOnlyList<string> violations =
            ProjectDependencyPolicy.FindViolations(actual, _allowedReferences);

        Assert.Empty(violations);
    }

    [Fact]
    public void PolicyRejectsForbiddenDomainToInfrastructureReference()
    {
        IReadOnlyDictionary<string, IReadOnlySet<string>> invalid =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["DecisionForge.Domain"] = Set("DecisionForge.Infrastructure"),
            };
        IReadOnlyDictionary<string, IReadOnlySet<string>> allowed =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["DecisionForge.Domain"] = Set(),
            };

        IReadOnlyList<string> violations =
            ProjectDependencyPolicy.FindViolations(invalid, allowed);

        string violation = Assert.Single(violations);
        Assert.Equal(
            "Project 'DecisionForge.Domain' must not reference 'DecisionForge.Infrastructure'.",
            violation);
    }

    private static HashSet<string> Set(params string[] values)
    {
        return values.ToHashSet(StringComparer.Ordinal);
    }
}
