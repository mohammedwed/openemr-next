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
}
