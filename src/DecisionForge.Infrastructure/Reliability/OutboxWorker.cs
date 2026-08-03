using DecisionForge.Application.Reliability.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DecisionForge.Infrastructure.Reliability;

public sealed partial class OutboxWorker(
    OutboxDispatcher dispatcher,
    IOptions<ReliabilityOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.DispatcherEnabled)
        {
            return;
        }

        TimeSpan pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                OutboxDispatchResult result = await dispatcher.DispatchOnceAsync(stoppingToken);
                if (result.TerminalFailures > 0)
                {
                    LogTerminalFailures(logger, result.TerminalFailures);
                }

                await dispatcher.CleanupCompletedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogCycleFailure(logger, exception);
            }

            await Task.Delay(pollInterval, timeProvider, stoppingToken);
        }
    }

    [LoggerMessage(
        EventId = 12001,
        Level = LogLevel.Error,
        Message = "Outbox dispatch reached {terminalFailureCount} terminal failures.")]
    private static partial void LogTerminalFailures(ILogger logger, int terminalFailureCount);

    [LoggerMessage(
        EventId = 12002,
        Level = LogLevel.Error,
        Message = "Outbox dispatch cycle failed with a controlled retry.")]
    private static partial void LogCycleFailure(ILogger logger, Exception exception);
}
