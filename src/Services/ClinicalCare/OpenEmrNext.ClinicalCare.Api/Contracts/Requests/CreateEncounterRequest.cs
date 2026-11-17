using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/encounters</c>. The bounds restate
/// <see cref="OpenEmrNext.ClinicalCare.Domain.ValueObjects.EncounterCreation.Create"/>'s own rules with the
/// <b>same messages</b> (ARC-11), so a malformed request fails at the binding boundary with the text the domain
/// would have produced. The domain factory remains the one place that actually enforces them; these attributes
/// fail the same checks earlier and feed the OpenAPI document, they do not replace the domain.
/// </summary>
/// <param name="PatientUuid">The patient's uuid, as <c>POST /api/v1/patients</c> returned it. Unknown patients get 404.</param>
/// <param name="ClassCode">Optional encounter class (legacy <c>_ActEncounterCode</c>, e.g. <c>AMB</c>); blank takes the configured default.</param>
/// <param name="CalendarCategoryId">Legacy <c>pc_catid</c>: the calendar category of the visit. Required.</param>
/// <param name="Date">
/// Optional encounter date and time; defaults to now. Stored as the server's local time, like legacy. A date <b>with</b> a zone
/// (<c>Z</c> or an offset) is an instant and is converted to that local time; a date <b>without</b> one is taken to be local already.
/// Must be between 1900-01-01 and one year from now.
/// </param>
/// <param name="Reason">Optional reason for the visit.</param>
/// <param name="FacilityId">Optional facility id; <c>0</c> for none. A non-zero id must exist.</param>
/// <param name="PosCode">Optional place-of-service code (1 to 99); defaults to the facility's.</param>
/// <param name="ProviderId">
/// The encounter's own provider (a field of the visit, not who is making the request). Optional: when omitted it is the signed-in user the gateway verified (ADR 0075). Who is acting is always
/// the verified user, whatever this says.
/// </param>
/// <param name="OnsetDate">Optional onset date of the condition the visit is about; the same zone and range rules as <paramref name="Date"/>.</param>
/// <param name="Sensitivity">Optional legacy sensitivity label (up to 30 characters). Stored, not yet enforced (finding F-CC-2).</param>
public sealed record CreateEncounterRequest(
    [Required] Guid PatientUuid,
    string? ClassCode,
    [Range(1, int.MaxValue, ErrorMessage = "A calendar category is required.")] int CalendarCategoryId,
    DateTime? Date,
    [StringLength(65_535, ErrorMessage = "Reason must be at most 65535 characters.")] string? Reason,
    [Range(0, int.MaxValue, ErrorMessage = "FacilityId cannot be negative.")] int FacilityId,
    [Range(1, 99, ErrorMessage = "PosCode must be between 1 and 99.")] int? PosCode,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long? ProviderId,
    DateTime? OnsetDate,
    [StringLength(30, ErrorMessage = "Sensitivity must be at most 30 characters.")] string? Sensitivity);
