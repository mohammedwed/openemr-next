namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

/// <summary>
/// One row of <c>GET /api/v1/patients</c> and the body of <c>GET /api/v1/patients/{uuid}</c>. A small
/// projection of <see cref="OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels.PatientFhirView"/>
/// -- the same read-model <c>GET /fhir/Patient</c>'s search already resolves -- for a caller that wants
/// a plain shape, not a FHIR Bundle/Patient.
/// </summary>
/// <param name="Uuid">The patient's public identity, the same one <c>POST /api/v1/patients</c> returns.</param>
/// <param name="Pid">OpenEMR's own internal join key -- needed to call the insurance/advance-directive
/// endpoints, which are keyed by it, not <paramref name="Uuid"/>.</param>
public sealed record PatientSummaryResponse(Guid Uuid, long Pid, string FirstName, string LastName, DateOnly? DateOfBirth, string? Email);
