namespace DecisionForge.ArchitectureTests;

internal static class ProjectDependencyPolicy
{
    public static IReadOnlyList<string> FindViolations(
        IReadOnlyDictionary<string, IReadOnlySet<string>> actualReferences,
        IReadOnlyDictionary<string, IReadOnlySet<string>> allowedReferences)
    {
        List<string> violations = [];

        foreach ((string project, IReadOnlySet<string> references) in actualReferences)
        {
            if (!allowedReferences.TryGetValue(project, out IReadOnlySet<string>? allowed))
            {
                violations.Add($"Project '{project}' is not part of the approved graph.");
                continue;
            }

            foreach (string reference in references.Except(allowed, StringComparer.Ordinal))
            {
                violations.Add($"Project '{project}' must not reference '{reference}'.");
            }

            foreach (string missing in allowed.Except(references, StringComparer.Ordinal))
            {
                violations.Add($"Project '{project}' must reference '{missing}'.");
            }
        }

        return violations.AsReadOnly();
    }
}
