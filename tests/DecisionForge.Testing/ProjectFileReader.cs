using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace DecisionForge.Testing;

public static class ProjectFileReader
{
    private const string _solutionFileName = "DecisionForge.sln";

    public static string FindRepositoryRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        DirectoryInfo? current = new(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, _solutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not find {_solutionFileName} from '{startDirectory}'.");
    }

    public static ProjectFileMetadata Read(string repositoryRoot, string relativeProjectPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeProjectPath);

        string projectPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativeProjectPath));
        string relativePath = Path.GetRelativePath(repositoryRoot, projectPath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The project path must remain inside the repository root.",
                nameof(relativeProjectPath));
        }

        XDocument project = XDocument.Load(projectPath, LoadOptions.None);
        string targetFramework = project.Descendants("TargetFramework").Single().Value;

        ReadOnlyCollection<string> projectReferences = Array.AsReadOnly(
            project.Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(
                    reference.Attribute("Include")?.Value))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .Order(StringComparer.Ordinal)
                .ToArray());

        ReadOnlyCollection<PackageReferenceMetadata> packageReferences = Array.AsReadOnly(
            project.Descendants("PackageReference")
                .Select(reference => new PackageReferenceMetadata(
                    reference.Attribute("Include")?.Value ?? string.Empty,
                    reference.Attribute("Version") is not null))
                .OrderBy(reference => reference.Name, StringComparer.Ordinal)
                .ToArray());

        return new ProjectFileMetadata(targetFramework, projectReferences, packageReferences);
    }
}
