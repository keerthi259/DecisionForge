namespace DecisionForge.Application.Reliability.Outbox;

public sealed record OutboxDispatcherOptions
{
    public int BatchSize { get; init; } = 20;

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan CompletedRetention { get; init; } = TimeSpan.FromDays(7);

    public void Validate()
    {
        if (BatchSize is < 1 or > 100)
        {
            throw new InvalidOperationException("Outbox batch size must be between 1 and 100.");
        }

        Positive(LeaseDuration, nameof(LeaseDuration));
        Positive(InitialRetryDelay, nameof(InitialRetryDelay));
        Positive(MaximumRetryDelay, nameof(MaximumRetryDelay));
        Positive(PollInterval, nameof(PollInterval));
        Positive(CompletedRetention, nameof(CompletedRetention));
        if (MaximumRetryDelay < InitialRetryDelay)
        {
            throw new InvalidOperationException(
                "Maximum retry delay cannot be shorter than initial retry delay.");
        }
    }

    private static void Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{name} must be positive.");
        }
    }
}
