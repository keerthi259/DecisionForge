namespace DecisionForge.Api.Configuration;

public sealed class PlatformOptions
{
    public const string SectionName = "DecisionForge:Platform";

    public string ApplicationName { get; init; } = string.Empty;

    public string CorrelationHeaderName { get; init; } = string.Empty;
}
