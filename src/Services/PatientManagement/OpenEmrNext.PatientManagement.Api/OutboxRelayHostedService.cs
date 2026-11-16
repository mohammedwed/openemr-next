using Microsoft.Extensions.Options;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Composition;

namespace OpenEmrNext.PatientManagement.Api;

/// <summary>
/// Runs <see cref="RelayOutboxUseCase"/> on a timer for as long as the host lives (EVT-7). It only schedules: every rule
/// about order, retry and dead-lettering is in the use case, where it can be tested without a clock.
/// </summary>
/// <remarks>
/// A failure of one pass is logged and the next pass runs; the relay never takes the host down, and the host never waits
/// on the relay to start. A dead-lettered event is logged at **error** level with its row id, which is the signal an alert
/// is built on (OPS-11).
/// </remarks>
public sealed class OutboxRelayHostedService(
    IServiceScopeFactory scopes, IOptions<EventRelayOptions> options, ILogger<OutboxRelayHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("The outbox relay is disabled (EventRelay:Enabled is false).");
            return;
        }

        logger.LogInformation("The outbox relay is running, every {Seconds}s, to {Target}.", settings.IntervalSeconds, settings.BaseUrl);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds));

        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<RelayOutboxUseCase>().ExecuteAsync(stoppingToken);

                if (result.Published > 0)
                    logger.LogInformation("Relayed {Published} event(s).", result.Published);
                if (result.Failed > 0)
                    logger.LogWarning("{Failed} event delivery failed and will be retried with back-off.", result.Failed);
                if (result.DeadLettered > 0)
                    logger.LogError("{Dead} event(s) were dead-lettered and need an operator: see last_error in patient_management_outbox.", result.DeadLettered);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A database blip must not kill the loop: log and try again next tick.
                logger.LogError(ex, "An outbox relay pass failed unexpectedly.");
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
