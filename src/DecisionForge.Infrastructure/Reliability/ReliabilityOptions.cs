namespace DecisionForge.Infrastructure.Reliability;

public sealed class ReliabilityOptions
{
    public const string SectionName = "DecisionForge:Reliability";

    public bool DispatcherEnabled { get; init; }

    public string MailpitBaseAddress { get; init; } = "http://localhost:8025";

    public string SenderEmail { get; init; } = "notifications@decisionforge.local";

    public int BatchSize { get; init; } = 20;

    public int PollIntervalSeconds { get; init; } = 2;

    public int CompletedRetentionDays { get; init; } = 7;

    public bool IsValid()
    {
        return Uri.TryCreate(MailpitBaseAddress, UriKind.Absolute, out Uri? address)
            && address.Scheme is "http" or "https"
            && Uri.CheckHostName(SenderEmail.Split('@').LastOrDefault() ?? string.Empty)
                != UriHostNameType.Unknown
            && SenderEmail.Count(character => character == '@') == 1
            && BatchSize is >= 1 and <= 100
            && PollIntervalSeconds is >= 1 and <= 300
            && CompletedRetentionDays is >= 1 and <= 365;
    }
}
