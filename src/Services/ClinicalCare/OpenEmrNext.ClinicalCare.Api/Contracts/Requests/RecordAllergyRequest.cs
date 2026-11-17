using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/allergies</c>. The bounds restate <see cref="OpenEmrNext.ClinicalCare.Domain.Allergies.AllergyRecording.Create"/>'s own rules
/// with the <b>same messages</b> (ARC-11), so a malformed request fails at the binding boundary with the text the domain would have produced. The domain
/// factory remains the one place that actually enforces them.
/// </summary>
/// <param name="PatientUuid">The patient's uuid, as <c>POST /api/v1/patients</c> returned it. Unknown patients get 404.</param>
/// <param name="Title">The substance, 2 to 255 characters. Required.</param>
/// <param name="BeginDate">
/// Optional (unlike a problem's): when the allergy began. Stored as the server's local time, like legacy. A date <b>with</b> a zone (<c>Z</c> or an offset) is an
/// instant and is converted to that local time; a date <b>without</b> one is taken to be local already. Between 1900-01-01 and one year from now.
/// </param>
/// <param name="EndDate">When it ended, or <c>null</c> while it is active. Not before <paramref name="BeginDate"/>; the same zone and range rules.</param>
/// <param name="Diagnosis">Optional coded diagnosis: <c>SYSTEM:code</c> pairs separated by semicolons.</param>
/// <param name="Reaction">Optional <c>reaction</c> list code (for example <c>hives</c>). Must be a configured code.</param>
/// <param name="Severity">Optional <c>severity_ccda</c> list code (for example <c>mild</c>, <c>severe</c>). Must be a configured code.</param>
/// <param name="Verification">Optional <c>allergyintolerance-verification</c> code (<c>unconfirmed</c>, <c>confirmed</c>, <c>refuted</c>, <c>entered-in-error</c>).</param>
/// <param name="Comments">Optional free-text comments.</param>
/// <param name="ProviderId">
/// The id of the acting provider. Caller-supplied for now and not verified against any signed-in user (ADR 0055, finding F-CC-1); revisit when identity
/// forwarding exists.
/// </param>
public sealed record RecordAllergyRequest(
    [Required] Guid PatientUuid,
    [Required(AllowEmptyStrings = false, ErrorMessage = "A title is required.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 255 characters.")] string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    [StringLength(255, ErrorMessage = "Diagnosis must be between 2 and 255 characters.")] string? Diagnosis,
    [StringLength(255, ErrorMessage = "Reaction is longer than a record can hold.")] string? Reaction,
    [StringLength(50, ErrorMessage = "Severity is longer than a record can hold.")] string? Severity,
    [StringLength(36, ErrorMessage = "Verification is longer than a record can hold.")] string? Verification,
    [StringLength(65_535, ErrorMessage = "Comments must be at most 65535 characters.")] string? Comments,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long ProviderId);
