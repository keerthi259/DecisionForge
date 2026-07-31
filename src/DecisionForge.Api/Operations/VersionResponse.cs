using System.Reflection;

namespace DecisionForge.Api.Operations;

public sealed record VersionResponse(string Application, string Version)
{
    public static VersionResponse Current { get; } = Create();

    private static VersionResponse Create()
    {
        Assembly assembly = typeof(Program).Assembly;
        string version = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        return new VersionResponse("DecisionForge.Api", version);
    }
}
