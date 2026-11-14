using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// The FHIR Patient API's <c>GET /fhir/Patient</c> search path -- the same ~15 parameters
/// <c>FhirPatientService::loadSearchParameters()</c> defines, combined with AND as exact-or-contains
/// matches (ADR 0051's own scope reduction, not replicated here); the actual SQL lives in the
/// adapter, this use case only forwards the already-parsed <see cref="PatientFhirSearchCriteria"/>.
/// </summary>
public sealed class SearchPatientFhirRecordsUseCase(IPatientFhirRepository repository)
{
    public Task<IReadOnlyList<PatientFhirView>> ExecuteAsync(PatientFhirSearchCriteria criteria, CancellationToken ct) =>
        repository.SearchAsync(criteria, ct);
}
