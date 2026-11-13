using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class SearchPatientFhirRecordsUseCase(IPatientFhirRepository repository)
{
    public Task<IReadOnlyList<PatientFhirView>> ExecuteAsync(PatientFhirSearchCriteria criteria, CancellationToken ct) =>
        repository.SearchAsync(criteria, ct);
}
