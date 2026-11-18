using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>Reads vitals back. Tolerant read models (DOM-9, ADR 0054), separate from the registry that persists strict aggregates.</summary>
public interface IVitalsReader
{
    /// <returns>The vitals, or <c>null</c> when this encounter has none with that uuid (or legacy has deleted them).</returns>
    Task<VitalsView?> FindAsync(EncounterReference encounter, Guid vitalsUuid, CancellationToken ct);

    /// <returns>One page of the encounter's non-deleted vitals, newest first (CV-14).</returns>
    Task<VitalsPage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct);

    /// <returns>The vitals with this uuid wherever they were taken, with their encounter and patient; <c>null</c> when none exists, it has been deleted, or its encounter or patient is not known here.</returns>
    Task<PatientVitals?> FindAnywhereAsync(Guid vitalsUuid, CancellationToken ct);

    /// <returns>At most <paramref name="max"/> of the patient's vitals sets across all their encounters, newest first. Bounded because the FHIR view counts and pages over the <i>measurements</i> in them, which the database cannot count.</returns>
    Task<IReadOnlyList<PatientVitals>> ListForPatientAsync(PatientReference patient, int max, CancellationToken ct);
}
