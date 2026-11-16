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
/// <param name="Date">Optional encounter date and time; defaults to now. Interpreted as the server's local time, like legacy.</param>
/// <param name="Reason">Optional reason for the visit.</param>
/// <param name="FacilityId">Optional facility id; <c>0</c> for none. A non-zero id must exist.</param>
/// <param name="PosCode">Optional place-of-service code (1 to 99); defaults to the facility's.</param>
/// <param name="ProviderId">
/// The id of the acting provider. Caller-supplied for now and not verified against any signed-in user (ADR
/// 0055, finding F-CC-1); revisit when identity forwarding exists.
/// </param>
/// <param name="OnsetDate">Optional onset date of the condition the visit is about.</param>
/// <param name="Sensitivity">Optional legacy sensitivity label (up to 30 characters). Stored, not yet enforced (finding F-CC-2).</param>
public sealed record CreateEncounterRequest(
    [Required] Guid PatientUuid,
    string? ClassCode,
    [Range(1, int.MaxValue, ErrorMessage = "A calendar category is required.")] int CalendarCategoryId,
    DateTime? Date,
    [StringLength(65_535, ErrorMessage = "Reason must be at most 65535 characters.")] string? Reason,
    [Range(0, int.MaxValue, ErrorMessage = "FacilityId cannot be negative.")] int FacilityId,
    [Range(1, 99, ErrorMessage = "PosCode must be between 1 and 99.")] int? PosCode,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long ProviderId,
    DateTime? OnsetDate,
    [StringLength(30, ErrorMessage = "Sensitivity must be at most 30 characters.")] string? Sensitivity);
