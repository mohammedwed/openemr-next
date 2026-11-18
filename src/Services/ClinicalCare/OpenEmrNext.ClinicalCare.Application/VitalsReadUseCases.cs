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

/// <summary>One vitals set by its uuid, wherever it was taken, with its encounter and patient (the FHIR Observation, ADR 0079).</summary>
public sealed class FindVitalsAnywhereUseCase(IVitalsReader reader)
{
    public Task<PatientVitals?> ExecuteAsync(Guid vitalsUuid, CancellationToken ct) => reader.FindAnywhereAsync(vitalsUuid, ct);
}

/// <summary>A patient's vitals sets across every encounter, newest first, bounded (ADR 0079). The patient has to be known here.</summary>
public sealed class ListPatientVitalsUseCase(IPatientDirectory patients, IVitalsReader reader)
{
    /// <summary>The most vitals sets one FHIR search looks at. A patient with more has their oldest sets out of reach of FHIR; the JSON API still lists them all, by encounter.</summary>
    public const int MaxSets = 1000;

    /// <exception cref="PatientNotFoundException">No patient has this uuid.</exception>
    public async Task<IReadOnlyList<PatientVitals>> ExecuteAsync(Guid patientUuid, CancellationToken ct)
    {
        var patient = await patients.FindAsync(patientUuid, ct) ?? throw new PatientNotFoundException(patientUuid);
        return await reader.ListForPatientAsync(patient, MaxSets, ct);
    }
}
