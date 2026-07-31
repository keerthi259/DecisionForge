namespace DecisionForge.Testing;

public sealed record PackageReferenceMetadata(string Name, bool SpecifiesVersion);

public sealed record ProjectFileMetadata(
    string TargetFramework,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageReferenceMetadata> PackageReferences);
