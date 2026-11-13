namespace OpenEmrNext.PatientManagement.Api.Contracts.Responses;

/// <summary>The body of a successful <c>POST /api/v1/patients</c>.</summary>
/// <param name="Uuid">
/// The patient's public identity -- the same column OpenEMR's own FHIR API already uses as
/// <c>Patient.id</c> (docs/m4-domain-notes.md). Callers that need to reference this patient again
/// should hold onto this, not <paramref name="Pid"/>.
/// </param>
/// <param name="Pid">OpenEMR's own internal join key. Exposed for compatibility with legacy tooling, not as the preferred identifier.</param>
/// <param name="Pubpid">The public patient ID legacy UIs display, defaulted to <paramref name="Pid"/>'s own value.</param>
public sealed record PatientRegisteredResponse(Guid Uuid, long Pid, string Pubpid);
