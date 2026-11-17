namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// A clinical note as a caller reads it: a query result, not an aggregate (ADR 0054), built tolerantly from whatever a legacy row holds (DOM-9). Blank text reads as <c>null</c>.
/// </summary>
/// <param name="Uuid">The public identity. A row without one is not addressable and never becomes a view (ADR 0070 #6).</param>
/// <param name="Date">The note's date.</param>
/// <param name="Type">The <c>Clinical_Note_Type</c> option id.</param>
/// <param name="Category">The <c>Clinical_Note_Category</c> option id, if any.</param>
/// <param name="Code">The LOINC code stored with the note (derived from its type when this service wrote it).</param>
/// <param name="CodeText">The label stored with the code.</param>
/// <param name="RecordedBy">The username legacy recorded as having written it; set on creation only.</param>
/// <param name="LastUpdated">When the row last changed, by any writer: the basis of the concurrency token (ADR 0068).</param>
public sealed record ClinicalNoteView(
    Guid Uuid, DateTime? Date, string? Type, string? Category, string? Description, string? Code, string? CodeText, string? RecordedBy, DateTime? LastUpdated);

/// <summary>One page of an encounter's clinical notes, newest first, with the total.</summary>
public sealed record ClinicalNotePage(IReadOnlyList<ClinicalNoteView> Items, int Total);
