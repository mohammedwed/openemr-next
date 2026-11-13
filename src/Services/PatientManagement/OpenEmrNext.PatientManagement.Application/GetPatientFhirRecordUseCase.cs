using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class GetPatientFhirRecordUseCase(IPatientFhirRepository repository)
{
    public Task<PatientFhirView?> ExecuteAsync(Guid uuid, CancellationToken ct) =>
        repository.GetByUuidAsync(uuid, ct);
}
