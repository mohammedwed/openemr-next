namespace OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

/// <summary>The body of a successful <c>POST /api/v1/encounters</c>.</summary>
/// <param name="Uuid">
/// The encounter's public identity (the <c>form_encounter.uuid</c> column OpenEMR's own FHIR API already uses
/// for <c>Encounter.id</c>). Callers that need this encounter again should hold this, not
/// <paramref name="Number"/>.
/// </param>
/// <param name="Number">
/// Legacy's encounter number (<c>form_encounter.encounter</c>), the key legacy screens and child tables join
/// on. Exposed for compatibility with legacy tooling, not as the preferred identifier.
/// </param>
/// <param name="PatientUuid">The patient the encounter belongs to.</param>
public sealed record EncounterCreatedResponse(Guid Uuid, long Number, Guid PatientUuid);
