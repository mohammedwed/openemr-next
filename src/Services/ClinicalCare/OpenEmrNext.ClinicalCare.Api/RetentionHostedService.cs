using Microsoft.Extensions.Options;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Composition;

namespace OpenEmrNext.ClinicalCare.Api;

/// <summary>
/// Runs <see cref="PurgeExpiredRecordsUseCase"/> on a timer (finding R-10). It only schedules; what is purged and for how long is
/// the use case's and the options', so it is tested without a clock. A failed run is logged and the next one tries again: purging is
/// housekeeping and must never take the host down.
/// </summary>
public sealed class RetentionHostedService(
    IServiceScopeFactory scopes, IOptions<RetentionOptions> options, ILogger<RetentionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Retention is disabled (Retention:Enabled is false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(settings.IntervalHours));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await scope.ServiceProvider.GetRequiredService<PurgeExpiredRecordsUseCase>().ExecuteAsync(stoppingToken);

                if (removed.Total > 0)
                    logger.LogInformation(
                        "Retention removed {Processed} processed event id(s), {Published} delivered and {Unpublished} undelivered outbox row(s).",
                        removed.ProcessedEvents, removed.PublishedOutbox, removed.UnpublishedOutbox);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A retention run failed unexpectedly; it will be retried at the next interval.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
