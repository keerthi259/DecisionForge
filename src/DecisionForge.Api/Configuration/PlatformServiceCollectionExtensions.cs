namespace DecisionForge.Api.Configuration;

public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddDecisionForgePlatform(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<PlatformOptions>()
            .Bind(configuration.GetRequiredSection(PlatformOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApplicationName),
                $"{PlatformOptions.SectionName}:ApplicationName is required.")
            .Validate(
                options => IsValidHeaderName(options.CorrelationHeaderName),
                $"{PlatformOptions.SectionName}:CorrelationHeaderName must be a valid HTTP header name.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsValidHeaderName(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 64
            && value.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }
}
