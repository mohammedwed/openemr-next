using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/problems</c>. The bounds restate <see cref="OpenEmrNext.ClinicalCare.Domain.Problems.ProblemRecording.Create"/>'s own rules
/// with the <b>same messages</b> (ARC-11), so a malformed request fails at the binding boundary with the text the domain would have produced. The domain
/// factory remains the one place that actually enforces them; these attributes fail the same checks earlier and feed the OpenAPI document.
/// </summary>
/// <param name="PatientUuid">The patient's uuid, as <c>POST /api/v1/patients</c> returned it. Unknown patients get 404.</param>
/// <param name="Title">The problem's name, 2 to 255 characters. Required.</param>
/// <param name="BeginDate">
/// When the problem began. Required. Stored as the server's local time, like legacy. A date <b>with</b> a zone (<c>Z</c> or an offset) is an instant and is
/// converted to that local time; a date <b>without</b> one is taken to be local already. Between 1900-01-01 and one year from now.
/// </param>
/// <param name="EndDate">When it ended, or <c>null</c> while it is active. Not before <paramref name="BeginDate"/>; the same zone and range rules.</param>
/// <param name="Diagnosis">Optional coded diagnosis: <c>SYSTEM:code</c> pairs separated by semicolons, for example <c>ICD10:J45.909;SNOMED-CT:195967001</c>.</param>
/// <param name="Outcome">An <c>outcome</c> list code (1 Resolved, 2 Improved, ...); <c>0</c> (the default) is unassigned. Must be a configured code.</param>
/// <param name="Occurrence">An <c>occurrence</c> list code (1 First, 4 Chronic/Recurrent, ...); <c>0</c> (the default) is unknown. Must be a configured code.</param>
/// <param name="ReferredBy">Optional: who referred the patient (up to 255 characters).</param>
/// <param name="Comments">Optional free-text comments.</param>
/// <param name="Verification">Optional <c>condition-verification</c> code (<c>unconfirmed</c>, <c>confirmed</c>, <c>refuted</c>, <c>entered-in-error</c>). Must be a configured code.</param>
/// <param name="ProviderId">
/// Ignored: the acting provider is the signed-in user the gateway verified (ADR 0075), so a caller can no longer name who is acting. Kept optional so existing callers do not break; it will be removed in a later contract version.
/// </param>
public sealed record RecordProblemRequest(
    [Required] Guid PatientUuid,
    [Required(AllowEmptyStrings = false, ErrorMessage = "A title is required.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 255 characters.")] string? Title,
    [Required(ErrorMessage = "A begin date is required.")] DateTime? BeginDate,
    DateTime? EndDate,
    [StringLength(255, ErrorMessage = "Diagnosis must be between 2 and 255 characters.")] string? Diagnosis,
    [Range(0, int.MaxValue, ErrorMessage = "Outcome must not be negative.")] int Outcome,
    [Range(0, int.MaxValue, ErrorMessage = "Occurrence must not be negative.")] int Occurrence,
    [StringLength(255, ErrorMessage = "Referred by must be at most 255 characters.")] string? ReferredBy,
    [StringLength(65_535, ErrorMessage = "Comments must be at most 65535 characters.")] string? Comments,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long? ProviderId,
    [StringLength(36, ErrorMessage = "Verification is longer than a record can hold.")] string? Verification = null);
