using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>PUT /api/v1/problems/{uuid}</c>: the problem's <b>whole editable set</b>, which replaces what is stored. Nothing defaults. The bounds restate
/// <see cref="OpenEmrNext.ClinicalCare.Domain.Problems.ProblemChange.Create"/>'s rules with the same messages (ARC-11); the domain stays the one place that enforces
/// them. The request must carry the <c>ETag</c> the caller read in an <c>If-Match</c> header: 428 without it, 412 if the record has since changed.
/// <para>
/// <b>To resolve a problem, send an <paramref name="EndDate"/></b> (and usually <paramref name="Outcome"/> 1); to reopen it, send none. Not editable here: the
/// patient, the uuid, who recorded it and when. The editor is not identified until identity forwarding exists (ADR 0069 #5), and the audit row says so.
/// </para>
/// </summary>
/// <param name="Title">The problem's name, 2 to 255 characters. Required.</param>
/// <param name="BeginDate">When it began. Required. The same zone and range rules as when it was recorded.</param>
/// <param name="EndDate">When it ended, or <c>null</c> while active. Not before <paramref name="BeginDate"/>.</param>
/// <param name="Diagnosis">Optional coded diagnosis, <c>SYSTEM:code</c> pairs separated by semicolons, or <c>null</c> to clear it.</param>
/// <param name="Outcome">An <c>outcome</c> list code (1 Resolved, ...); <c>0</c> is unassigned.</param>
/// <param name="Occurrence">An <c>occurrence</c> list code; <c>0</c> is unknown.</param>
/// <param name="ReferredBy">Who referred the patient, or <c>null</c>.</param>
/// <param name="Comments">Free-text comments, or <c>null</c>.</param>
/// <param name="Verification">A <c>condition-verification</c> code, or <c>null</c> for none.</param>
public sealed record UpdateProblemRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "A title is required.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 255 characters.")] string? Title,
    [Required(ErrorMessage = "A begin date is required.")] DateTime? BeginDate,
    DateTime? EndDate,
    [StringLength(255, ErrorMessage = "Diagnosis must be between 2 and 255 characters.")] string? Diagnosis,
    [Range(0, int.MaxValue, ErrorMessage = "Outcome must not be negative.")] int Outcome,
    [Range(0, int.MaxValue, ErrorMessage = "Occurrence must not be negative.")] int Occurrence,
    [StringLength(255, ErrorMessage = "Referred by must be at most 255 characters.")] string? ReferredBy,
    [StringLength(65_535, ErrorMessage = "Comments must be at most 65535 characters.")] string? Comments,
    [StringLength(36, ErrorMessage = "Verification is longer than a record can hold.")] string? Verification);

/// <summary>
/// The body of <c>PUT /api/v1/allergies/{uuid}</c>: the allergy's <b>whole editable set</b>, which replaces what is stored (the same contract as
/// <see cref="UpdateProblemRequest"/>). <b>To end an allergy, send an <paramref name="EndDate"/></b>; to reopen it, send none.
/// </summary>
/// <param name="Title">The substance, 2 to 255 characters. Required.</param>
/// <param name="BeginDate">When it began, or <c>null</c>: unlike a problem's, optional.</param>
/// <param name="EndDate">When it ended, or <c>null</c> while active.</param>
/// <param name="Diagnosis">Optional coded diagnosis, or <c>null</c> to clear it.</param>
/// <param name="Reaction">A <c>reaction</c> list code, or <c>null</c> for none.</param>
/// <param name="Severity">A <c>severity_ccda</c> list code, or <c>null</c> for none.</param>
/// <param name="Verification">An <c>allergyintolerance-verification</c> code, or <c>null</c> for none.</param>
/// <param name="Comments">Free-text comments, or <c>null</c>.</param>
public sealed record UpdateAllergyRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "A title is required.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Title must be between 2 and 255 characters.")] string? Title,
    DateTime? BeginDate,
    DateTime? EndDate,
    [StringLength(255, ErrorMessage = "Diagnosis must be between 2 and 255 characters.")] string? Diagnosis,
    [StringLength(255, ErrorMessage = "Reaction is longer than a record can hold.")] string? Reaction,
    [StringLength(50, ErrorMessage = "Severity is longer than a record can hold.")] string? Severity,
    [StringLength(36, ErrorMessage = "Verification is longer than a record can hold.")] string? Verification,
    [StringLength(65_535, ErrorMessage = "Comments must be at most 65535 characters.")] string? Comments);
