using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>What the FHIR Patient use cases need from persistence: a single patient resolved for
/// rendering, or a set matching search criteria. Both reads only -- creating/updating a patient still
/// goes through <see cref="IPatientRegistry"/>, the same path plain registration already uses.</summary>
public interface IPatientFhirRepository
{
    Task<PatientFhirView?> GetByUuidAsync(Guid uuid, CancellationToken ct);

    /// <summary>One bounded page of the patients matching <paramref name="criteria"/>, in a stable order, with the total that matched.</summary>
    Task<PatientFhirSearchResult> SearchAsync(PatientFhirSearchCriteria criteria, CancellationToken ct);
}
