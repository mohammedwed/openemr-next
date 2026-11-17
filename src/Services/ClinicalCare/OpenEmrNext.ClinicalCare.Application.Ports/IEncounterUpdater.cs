using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// Everything one update must commit together: the change, the version it is based on, the event and the audit row. The adapter owns the
/// transaction (EVT-1, SEC-9); the use case only decides what should happen.
/// </summary>
/// <param name="Expected">The version the caller read. The write applies only if the row still has it (LEG-10).</param>
public sealed record EncounterUpdateRequest(
    Guid EncounterUuid, ConcurrencyToken Expected, EncounterChange Change, EncounterUpdatedEvent Event, AuditEntry Audit);

/// <summary>
/// What <c>UpdateEncounterUseCase</c> needs from persistence: a conditional update of the columns this service owns (ADR 0068), committed
/// with its <see cref="EncounterUpdatedEvent"/> and its audit row, atomically, without the use case knowing how.
/// </summary>
public interface IEncounterUpdater
{
    /// <summary>
    /// Applies <see cref="EncounterUpdateRequest.Change"/> if and only if the row still has the expected version. If it returns anything
    /// but <see cref="UpdateStatus.Updated"/>, nothing was written.
    /// </summary>
    Task<UpdateOutcome> UpdateAsync(EncounterUpdateRequest request, CancellationToken ct);
}
