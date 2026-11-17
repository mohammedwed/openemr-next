using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Domain.VitalSigns;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Reads one set of vitals of an encounter.</summary>
public sealed class GetVitalsUseCase(IEncounterReader encounters, IVitalsReader reader)
{
    /// <returns>The vitals, or <c>null</c> when this encounter has no such vitals.</returns>
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    public async Task<VitalsView?> ExecuteAsync(Guid encounterUuid, Guid vitalsUuid, CancellationToken ct)
    {
        var view = await encounters.FindAsync(encounterUuid, ct) ?? throw new EncounterNotFoundException(encounterUuid);

        // A half-filled legacy encounter (no number or patient) cannot have vitals linked to it, so it has none.
        return EncounterReference.TryCreate(view.Uuid, view.Number, view.PatientUuid, view.PatientPid) is { } encounter
            ? await reader.FindAsync(encounter, vitalsUuid, ct)
            : null;
    }
}

/// <summary>Lists an encounter's vitals, newest first, one bounded page at a time (API-6).</summary>
public sealed class ListEncounterVitalsUseCase(IEncounterReader encounters, IVitalsReader reader)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    /// <exception cref="ClinicalCareValidationException">The limit or offset is out of bounds.</exception>
    public async Task<VitalsPage> ExecuteAsync(Guid encounterUuid, int? limit, int? offset, CancellationToken ct)
    {
        var page = PageRequest.Create(limit, offset);

        var view = await encounters.FindAsync(encounterUuid, ct) ?? throw new EncounterNotFoundException(encounterUuid);

        return EncounterReference.TryCreate(view.Uuid, view.Number, view.PatientUuid, view.PatientPid) is { } encounter
            ? await reader.ListForEncounterAsync(encounter, page, ct)
            : new VitalsPage([], 0);
    }
}
