using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Lists a patient's encounters, newest first, one bounded page at a time (API-6).</summary>
public sealed class ListPatientEncountersUseCase(IPatientDirectory patients, IEncounterReader reader)
{
    /// <exception cref="PatientNotFoundException">No patient has this uuid.</exception>
    /// <exception cref="EncounterValidationException">The limit or offset is out of bounds.</exception>
    public async Task<EncounterPage> ExecuteAsync(Guid patientUuid, int? limit, int? offset, CancellationToken ct)
    {
        // Bounds first: a caller asking for 10,000 rows should be told so without a database round trip.
        var page = PageRequest.Create(limit, offset);

        var patient = await patients.FindAsync(patientUuid, ct)
            ?? throw new PatientNotFoundException(patientUuid);

        return await reader.ListForPatientAsync(patient, page, ct);
    }
}
