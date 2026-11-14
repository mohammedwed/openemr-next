using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class GetPatientFhirRecordUseCase(IPatientFhirRepository repository)
{
    public Task<PatientFhirView?> ExecuteAsync(Guid uuid, CancellationToken ct) =>
        repository.GetByUuidAsync(uuid, ct);
}
