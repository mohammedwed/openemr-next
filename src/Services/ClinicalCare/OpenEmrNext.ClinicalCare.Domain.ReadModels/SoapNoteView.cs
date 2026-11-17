using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// A SOAP note as a caller reads it: a query result, not an aggregate (ADR 0054), built tolerantly from whatever a legacy row holds (DOM-9). Blank sections read as <c>null</c>.
/// </summary>
/// <param name="Id">The note's public identity within its encounter (ADR 0073).</param>
/// <param name="Date">When it was written; <c>null</c> if the row has none.</param>
/// <param name="RecordedBy">The username legacy recorded as having written it, if any.</param>
public sealed record SoapNoteView(long Id, DateTime? Date, string? RecordedBy, string? Subjective, string? Objective, string? Assessment, string? Plan)
{
    /// <summary>The note's concurrency version (ADR 0072, 0073): a hash of the four sections, because the table has no stamp. Who wrote it and when are not in it: nothing here can change them.</summary>
    public ContentVersion Version() => ContentVersion.Of([Subjective, Objective, Assessment, Plan]);
}

/// <summary>One page of an encounter's SOAP notes, newest first, with the total.</summary>
public sealed record SoapNotePage(IReadOnlyList<SoapNoteView> Items, int Total);
