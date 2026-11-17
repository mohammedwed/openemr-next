namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>How long each kind of bookkeeping record is kept, in days. See <c>RetentionOptions</c> for why these values.</summary>
public sealed record RetentionCutoffs(int ProcessedEventDays, int PublishedOutboxDays, int UnpublishedOutboxDays);

/// <summary>How many rows one batch removed, per table.</summary>
public sealed record PurgeResult(int ProcessedEvents, int PublishedOutbox, int UnpublishedOutbox)
{
    public int Total => ProcessedEvents + PublishedOutbox + UnpublishedOutbox;

    public static PurgeResult Nothing { get; } = new(0, 0, 0);

    public static PurgeResult operator +(PurgeResult a, PurgeResult b) =>
        new(a.ProcessedEvents + b.ProcessedEvents, a.PublishedOutbox + b.PublishedOutbox, a.UnpublishedOutbox + b.UnpublishedOutbox);
}

/// <summary>
/// Removes the service's own bookkeeping once it can no longer matter (EVT-7, finding R-10). Only tables this service owns: it never
/// touches a legacy table, and clinical data (encounters, vitals) is never purged by anything here.
/// </summary>
public interface IRetentionStore
{
    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> rows from each table that is past its cutoff, measured by the **database's own
    /// clock**, so hosts with different clocks agree. Small batches keep each delete short, so purging never holds locks that
    /// delay a real request.
    /// </summary>
    Task<PurgeResult> PurgeBatchAsync(RetentionCutoffs cutoffs, int batchSize, CancellationToken ct);
}
