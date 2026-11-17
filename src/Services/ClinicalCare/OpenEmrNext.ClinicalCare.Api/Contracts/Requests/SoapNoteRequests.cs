using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/encounters/{encounterUuid}/soap-notes</c> (M5d slice 3, ADR 0073). At least one section must have text. A section that is given has at least 2 characters and at most 65,535 <b>bytes</b>
/// (the column is <c>text</c>): the domain enforces both, with the messages in the response.
/// </summary>
/// <param name="Subjective">What the patient reports.</param>
/// <param name="Objective">What was observed and measured.</param>
/// <param name="Assessment">The clinician's assessment.</param>
/// <param name="Plan">The plan.</param>
/// <param name="ProviderId">Ignored: the acting provider is the signed-in user the gateway verified (ADR 0075), so a caller can no longer name who is acting. Kept optional so existing callers do not break; it will be removed in a later contract version.; the signed-in user's username and group are recorded on the legacy rows.</param>
public sealed record RecordSoapNoteRequest(
    string? Subjective, string? Objective, string? Assessment, string? Plan,
    [Range(1, int.MaxValue, ErrorMessage = "ProviderId must identify the acting provider.")] long? ProviderId);

/// <summary>
/// The body of <c>PUT /api/v1/encounters/{encounterUuid}/soap-notes/{noteId}</c>: the <b>whole</b> set of sections, replacing what is stored (a section left out is cleared), conditional on the <c>ETag</c> in <c>If-Match</c>.
/// </summary>
public sealed record CorrectSoapNoteRequest(string? Subjective, string? Objective, string? Assessment, string? Plan);
