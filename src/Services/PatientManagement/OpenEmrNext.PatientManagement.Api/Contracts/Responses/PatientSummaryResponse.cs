namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

/// <summary>
/// One row of <c>GET /api/v1/patients</c>. A small projection of <see cref="OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels.PatientFhirView"/>
/// -- the same read-model <c>GET /fhir/Patient</c>'s search already resolves -- for a caller that wants
/// a plain list, not a FHIR Bundle.
/// </summary>
/// <param name="Uuid">The patient's public identity, the same one <c>POST /api/v1/patients</c> returns.</param>
public sealed record PatientSummaryResponse(Guid Uuid, string FirstName, string LastName, DateOnly? DateOfBirth, string? Email);
