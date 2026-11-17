using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/encounters/{encounterUuid}/clinical-notes</c> (M5d slice 3b, ADR 0074).
/// </summary>
/// <param name="Type">A configured note type (<c>GET /api/v1/lookups/clinical-note-types</c>), for example <c>progress_note</c>. Required. The note's LOINC code is derived from it.</param>
/// <param name="Category">A configured note category, or <c>null</c>.</param>
/// <param name="Description">The note. Required, at least 2 characters and at most 65,535 <b>bytes</b>; kept exactly as written.</param>
/// <param name="Date">The note's date (a calendar date, not in the future). Defaults to today.</param>
/// <param name="ProviderId">Ignored: the acting provider is the signed-in user the gateway verified (ADR 0075), so a caller can no longer name who is acting. Kept optional so existing callers do not break; it will be removed in a later contract version.; the signed-in user's username is recorded as who wrote the note.</param>
public sealed record RecordClinicalNoteRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "A note type is required.")] string? Type,
    string? Category,
    string? Description,
    DateTime? Date,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long? ProviderId);

/// <summary>
/// The body of <c>PUT /api/v1/encounters/{encounterUuid}/clinical-notes/{noteUuid}</c>: the <b>whole</b> editable set, replacing what is stored (a category left out is cleared), conditional on the <c>ETag</c> in
/// <c>If-Match</c>. The date is required here, so a correction cannot silently re-date a note.
/// </summary>
public sealed record CorrectClinicalNoteRequest(
    [Required(AllowEmptyStrings = false, ErrorMessage = "A note type is required.")] string? Type,
    string? Category,
    string? Description,
    [Required(ErrorMessage = "A note date is required.")] DateTime? Date);
