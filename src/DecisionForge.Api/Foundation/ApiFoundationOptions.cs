namespace DecisionForge.Api.Foundation;

public sealed class ApiFoundationOptions
{
    public const string SectionName = "DecisionForge:Api";
    public const long DefaultMaximumRequestBodyBytes = 262_144;

    public long MaximumRequestBodyBytes { get; init; } = DefaultMaximumRequestBodyBytes;

    public string[] AllowedCorsOrigins { get; init; } = [];

    public bool IsValid()
    {
        return MaximumRequestBodyBytes is >= 1_024 and <= 1_048_576
            && AllowedCorsOrigins is not null
            && AllowedCorsOrigins.Length <= 10
            && AllowedCorsOrigins.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                == AllowedCorsOrigins.Length
            && AllowedCorsOrigins.All(IsAllowedOrigin);
    }

    private static bool IsAllowedOrigin(string origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
            && uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && (uri.Scheme == Uri.UriSchemeHttps
                || uri is { Scheme: "http", IsLoopback: true });
    }
}
