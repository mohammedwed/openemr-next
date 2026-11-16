namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// What the relay needs from the outbox table (EVT-7): claim rows in order, then record each outcome. A claim is a
/// lease, not a delete, so a relay that dies mid-batch loses nothing: the lease expires and the rows are claimed again.
/// </summary>
public interface IOutboxRelayStore
{
    /// <summary>
    /// Leases up to <paramref name="batchSize"/> unpublished, not-dead-lettered rows **in id order**, stopping at the first
    /// one that is not yet due (in back-off) or is leased by another relay, so a later row never overtakes an earlier one.
    /// </summary>
    Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int batchSize, TimeSpan lease, CancellationToken ct);

    Task MarkPublishedAsync(long id, CancellationToken ct);

    /// <summary>Records a failed attempt and, unless <paramref name="deadLetter"/>, schedules the next one after <paramref name="retryAfter"/>.</summary>
    Task MarkFailedAsync(long id, string error, TimeSpan retryAfter, bool deadLetter, CancellationToken ct);

    /// <summary>Gives leased rows back untouched, for rows the relay claimed but did not try (a failure ahead of them stopped the batch).</summary>
    Task ReleaseAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
}
