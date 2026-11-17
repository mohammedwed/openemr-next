using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// Everything one update must commit together: the change, the version it is based on, the event and the audit row. The adapter owns the
/// transaction (EVT-1, SEC-9); the use case only decides what should happen.
/// </summary>
/// <param name="Expected">The version the caller read. The write applies only if the row still has it (LEG-10).</param>
public sealed record EncounterUpdateRequest(
    Guid EncounterUuid, ConcurrencyToken Expected, EncounterChange Change, EncounterUpdatedEvent Event, AuditEntry Audit);

/// <summary>How an update attempt ended.</summary>
public enum EncounterUpdateStatus
{
    /// <summary>The row, its event and its audit row were committed.</summary>
    Updated,

    /// <summary>The row exists but is no longer the version the caller read. Nothing was written.</summary>
    Conflict,

    /// <summary>No such encounter, or legacy deleted it. Nothing was written.</summary>
    NotFound
}

/// <param name="Status">How the attempt ended.</param>
/// <param name="Stamp">For <see cref="EncounterUpdateStatus.Updated"/>, the row's new stamp; for <see cref="EncounterUpdateStatus.Conflict"/>, its current one.</param>
public sealed record EncounterUpdateOutcome(EncounterUpdateStatus Status, DateTime? Stamp);

/// <summary>
/// What <c>UpdateEncounterUseCase</c> needs from persistence: a conditional update of the columns this service owns (ADR 0068), committed
/// with its <see cref="EncounterUpdatedEvent"/> and its audit row, atomically, without the use case knowing how.
/// </summary>
public interface IEncounterUpdater
{
    /// <summary>
    /// Applies <see cref="EncounterUpdateRequest.Change"/> if and only if the row still has the expected version. If it returns anything
    /// but <see cref="EncounterUpdateStatus.Updated"/>, nothing was written.
    /// </summary>
    Task<EncounterUpdateOutcome> UpdateAsync(EncounterUpdateRequest request, CancellationToken ct);
}
