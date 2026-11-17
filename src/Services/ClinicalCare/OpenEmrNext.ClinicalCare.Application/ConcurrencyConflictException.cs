namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// The record changed after the caller read it (LEG-10, ADR 0068): another writer, possibly a legacy screen or a billing action, got there first.
/// Maps to 412, with the current version so the caller can reload and try again.
/// </summary>
/// <param name="recordUuid">The record the caller tried to change.</param>
/// <param name="currentStamp">The record's current stamp, or <c>null</c> when it has none to offer.</param>
public sealed class ConcurrencyConflictException(Guid recordUuid, DateTime? currentStamp)
    : Exception($"{recordUuid} changed after it was read.")
{
    public Guid RecordUuid { get; } = recordUuid;

    public DateTime? CurrentStamp { get; } = currentStamp;
}
