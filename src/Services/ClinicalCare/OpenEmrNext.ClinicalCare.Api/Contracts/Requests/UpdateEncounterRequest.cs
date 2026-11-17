using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>PUT /api/v1/encounters/{uuid}</c>: the encounter's <b>whole editable set</b>, which replaces what is stored. Unlike a create,
/// nothing defaults: a missing class or date is an error, because a <c>PUT</c> that silently filled them in would rewrite a field the caller never
/// mentioned. The bounds restate <see cref="OpenEmrNext.ClinicalCare.Domain.ValueObjects.EncounterChange.Create"/>'s rules with the same messages
/// (ARC-11); the domain stays the one place that enforces them.
/// <para>
/// Not editable here: the patient, the encounter's number, its uuid, <c>sensitivity</c> (finding F-CC-2) and every billing or collection field.
/// The request must carry the <c>ETag</c> the caller read in an <c>If-Match</c> header: 428 without it, 412 if the record has since changed.
/// </para>
/// </summary>
/// <param name="ClassCode">The encounter class (legacy <c>_ActEncounterCode</c>, e.g. <c>AMB</c>). Required.</param>
/// <param name="CalendarCategoryId">Legacy <c>pc_catid</c>: the calendar category of the visit. Required.</param>
/// <param name="Date">
/// The encounter date and time. Required. Stored as the server's local time, like legacy. A date <b>with</b> a zone (<c>Z</c> or an offset) is an
/// instant and is converted to that local time; a date <b>without</b> one is taken to be local already. Between 1900-01-01 and one year from now.
/// </param>
/// <param name="OnsetDate">The onset date of the condition the visit is about, or <c>null</c> for none; the same zone and range rules as <paramref name="Date"/>.</param>
/// <param name="Reason">The reason for the visit, or <c>null</c> to clear it.</param>
/// <param name="FacilityId">The facility id; <c>0</c> for none. A non-zero id must exist.</param>
/// <param name="PosCode">The place-of-service code (1 to 99), or <c>null</c> to take the facility's.</param>
/// <param name="ProviderId">
/// The id of the provider the encounter belongs to <b>after</b> this change. It says whose encounter it is, not who is editing it: the editor is not
/// identified until identity forwarding exists (ADR 0069 #5), and the audit row says so.
/// </param>
public sealed record UpdateEncounterRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "A class is required.")] string? ClassCode,
    [Range(1, int.MaxValue, ErrorMessage = "A calendar category is required.")] int CalendarCategoryId,
    [Required(ErrorMessage = "A date is required.")] DateTime? Date,
    DateTime? OnsetDate,
    [StringLength(65_535, ErrorMessage = "Reason must be at most 65535 characters.")] string? Reason,
    [Range(0, int.MaxValue, ErrorMessage = "FacilityId cannot be negative.")] int FacilityId,
    [Range(1, 99, ErrorMessage = "PosCode must be between 1 and 99.")] int? PosCode,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the encounter's provider.")] long ProviderId);
