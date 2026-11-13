using OpenEmrNext.PatientManagement.Api.Contracts.Fhir;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>
/// Body of <c>POST /fhir/Patient</c> -- only the subset of the full <see cref="FhirPatient"/> shape
/// <see cref="Endpoints.FhirPatientEndpoints"/> actually extracts for creation. Matches legacy's own
/// <c>parseFhirResource()</c>, including its documented limitation: race/ethnicity/language are read
/// on output but never mapped back on input (legacy's own TODO: "ONC certification only deals with
/// READ operations... the creation/updating of resources is currently NOT correct").
/// </summary>
public sealed record CreateFhirPatientRequest(List<FhirHumanName>? Name, List<FhirContactPoint>? Telecom, string? Gender, string? BirthDate);
