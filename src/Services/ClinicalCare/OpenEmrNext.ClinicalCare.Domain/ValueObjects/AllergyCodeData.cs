namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The configured code lists an allergy's coded fields are checked against, read beforehand by the use case so the domain does no I/O (DOM-7).
/// Unlike a problem's integer codes, legacy stores these as the string <c>option_id</c>s of <c>list_options</c> entries, compared exactly (as class codes are).
/// </summary>
/// <param name="Reactions">Valid <c>lists.reaction</c> codes (list <c>reaction</c>: <c>hives</c>, <c>nausea</c>, ...).</param>
/// <param name="Severities">Valid <c>lists.severity_al</c> codes (list <c>severity_ccda</c>: <c>mild</c>, <c>moderate</c>, <c>severe</c>, ...).</param>
/// <param name="Verifications">Valid <c>lists.verification</c> codes (list <c>allergyintolerance-verification</c>: <c>unconfirmed</c>, <c>confirmed</c>, <c>refuted</c>, <c>entered-in-error</c>).</param>
public sealed record AllergyCodeData(
    IReadOnlySet<string> Reactions, IReadOnlySet<string> Severities, IReadOnlySet<string> Verifications);
