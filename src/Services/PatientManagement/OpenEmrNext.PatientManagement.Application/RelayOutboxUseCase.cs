using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>How the relay behaves when delivery fails (EVT-7). Plain settings, bound at the composition root.</summary>
/// <param name="BatchSize">Rows claimed per pass.</param>
/// <param name="MaxAttempts">After this many failed attempts a row is dead-lettered and the relay moves on (and logs it, for an alert).</param>
/// <param name="Lease">How long a claimed row is held; must comfortably exceed one delivery.</param>
/// <param name="BaseBackoff">The wait after the first failure; it doubles per attempt.</param>
/// <param name="MaxBackoff">The longest wait between attempts.</param>
public sealed record RelayPolicy(int BatchSize, int MaxAttempts, TimeSpan Lease, TimeSpan BaseBackoff, TimeSpan MaxBackoff)
{
    public static RelayPolicy Default { get; } =
        new(BatchSize: 20, MaxAttempts: 8, Lease: TimeSpan.FromMinutes(1), BaseBackoff: TimeSpan.FromSeconds(5), MaxBackoff: TimeSpan.FromMinutes(5));

    /// <summary>The wait after the <paramref name="attempt"/>th failed attempt: exponential, capped.</summary>
    public TimeSpan BackoffAfter(int attempt)
    {
        var factor = Math.Pow(2, Math.Max(0, attempt - 1));
        var seconds = Math.Min(BaseBackoff.TotalSeconds * factor, MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}

/// <summary>What one pass of the relay did, for logging and tests.</summary>
public sealed record RelayResult(int Published, int Failed, int DeadLettered)
{
    public static RelayResult Nothing { get; } = new(0, 0, 0);
}

/// <summary>
/// Drains the outbox (EVT-7): claim rows in order, deliver each, record the outcome. Order is kept by stopping the pass at
/// the first failure, so a later event never overtakes an earlier one; only a **dead-lettered** row (an operator-level
/// event, logged) is skipped. Delivery is at-least-once: a crash between delivering and marking leaves the row to be
/// delivered again, and the consumer's idempotency on the event id makes that harmless.
/// </summary>
public sealed class RelayOutboxUseCase(IOutboxRelayStore store, IEventPublisher publisher, RelayPolicy policy)
{
    public async Task<RelayResult> ExecuteAsync(CancellationToken ct)
    {
        var claimed = await store.ClaimAsync(policy.BatchSize, policy.Lease, ct);
        if (claimed.Count == 0)
            return RelayResult.Nothing;

        int published = 0, failed = 0, deadLettered = 0;

        for (var i = 0; i < claimed.Count; i++)
        {
            var entry = claimed[i];
            try
            {
                await publisher.PublishAsync(OutboundEventFactory.From(entry), ct);
                await store.MarkPublishedAsync(entry.Id, ct);
                published++;
            }
            catch (Exception ex) when (ex is EventPublishException or UnpublishableEventException)
            {
                var permanent = ex is UnpublishableEventException || ((EventPublishException)ex).IsPermanent;
                var attempt = entry.Attempts + 1;
                var dead = permanent || attempt >= policy.MaxAttempts;

                await store.MarkFailedAsync(entry.Id, Truncate(ex.Message), policy.BackoffAfter(attempt), dead, ct);

                if (dead)
                {
                    deadLettered++;
                    continue; // the row is out of the way; the rest of the batch may go on
                }

                failed++;
                // Order matters more than throughput: stop here, give back what was claimed but not tried.
                await store.ReleaseAsync(claimed.Skip(i + 1).Select(e => e.Id).ToList(), ct);
                break;
            }
        }

        return new RelayResult(published, failed, deadLettered);
    }

    private static string Truncate(string message) => message.Length <= 900 ? message : message[..900];
}
