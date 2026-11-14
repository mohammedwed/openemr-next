using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// The FHIR Patient API's <c>GET /fhir/Patient/{uuid}</c> read path -- a thin pass-through to
/// <see cref="IPatientFhirRepository"/>, since every real lookup/resolution decision
/// (<c>list_options</c> codes, legacy's "omit vs. default to Unknown" distinction) already lives in
/// the adapter that builds a <c>PatientFhirView</c>, not here.
/// </summary>
public sealed class GetPatientFhirRecordUseCase(IPatientFhirRepository repository)
{
    public Task<PatientFhirView?> ExecuteAsync(Guid uuid, CancellationToken ct) =>
        repository.GetByUuidAsync(uuid, ct);
}
