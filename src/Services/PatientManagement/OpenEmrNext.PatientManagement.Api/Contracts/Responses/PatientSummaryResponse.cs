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
/// <param name="PublicPatientId">The public patient ID (<c>pubpid</c>) that OpenEMR's own screens show as
/// the patient's identifier. Distinct from <paramref name="Pid"/>; may differ for patients imported from
/// legacy data. Not an MRN: this service does not model one.</param>
public sealed record PatientSummaryResponse(
    Guid Uuid, long Pid, string FirstName, string LastName, DateOnly? DateOfBirth, string? Email, string? PublicPatientId, string? ETag = null, string? Sex = null, string? TrustedEmail = null);
