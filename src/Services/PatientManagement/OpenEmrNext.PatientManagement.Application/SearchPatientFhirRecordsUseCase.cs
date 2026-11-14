using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class SearchPatientFhirRecordsUseCase(IPatientFhirRepository repository)
{
    public Task<IReadOnlyList<PatientFhirView>> ExecuteAsync(PatientFhirSearchCriteria criteria, CancellationToken ct) =>
        repository.SearchAsync(criteria, ct);
}
