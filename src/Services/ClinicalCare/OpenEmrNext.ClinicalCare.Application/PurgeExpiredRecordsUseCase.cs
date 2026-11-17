using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// Runs retention (finding R-10): repeats a small batch purge until nothing more is due, so a large backlog is cleared in many
/// short deletes and never one long one. The loop is bounded, so a misbehaving store cannot spin forever.
/// </summary>
public sealed class PurgeExpiredRecordsUseCase(IRetentionStore store, RetentionCutoffs cutoffs, int batchSize)
{
    /// <summary>The most batches one run will do; at the default batch size that is 50,000 rows per table, per run.</summary>
    public const int MaxBatchesPerRun = 50;

    public async Task<PurgeResult> ExecuteAsync(CancellationToken ct)
    {
        var total = PurgeResult.Nothing;

        for (var batch = 0; batch < MaxBatchesPerRun; batch++)
        {
            var removed = await store.PurgeBatchAsync(cutoffs, batchSize, ct);
            total += removed;

            // A table that returned less than a full batch has nothing left that is due; stop when none returned a full one.
            if (removed.ProcessedEvents < batchSize && removed.PublishedOutbox < batchSize && removed.UnpublishedOutbox < batchSize)
                break;
        }

        return total;
    }
}
