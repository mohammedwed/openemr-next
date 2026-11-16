using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// Reads encounters back. Separate from <see cref="IEncounterRegistry"/> because reads return tolerant read
/// models, built from rows any legacy writer may have created, while the registry persists strict aggregates
/// (DOM-9, ADR 0054).
/// </summary>
public interface IEncounterReader
{
    /// <returns>
    /// The encounter, or <c>null</c> when none has this uuid <b>or it has been deleted</b>: legacy deletes by
    /// flagging its <c>forms</c> row, so a deleted encounter is treated as absent (CC-9).
    /// </returns>
    Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct);

    /// <returns>One page of the patient's encounters, newest first, soft-deleted ones excluded (CC-9).</returns>
    Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct);
}
