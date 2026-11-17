namespace OpenEmrNext.ClinicalCare.Domain.IssueLinks;

/// <summary>Which of the issue types this service owns an issue is (ADR 0070 #1). A medication, surgery or any other type is not one: it is not addressable here.</summary>
public enum IssueKind
{
    /// <summary>A problem: a <c>lists</c> row of type <c>medical_problem</c>.</summary>
    Problem,

    /// <summary>An allergy: a <c>lists</c> row of type <c>allergy</c>.</summary>
    Allergy
}

/// <summary>
/// An issue this service owns, as a link needs to know it: its public uuid, its row id (legacy's <c>issue_encounter.list_id</c> joins on the id, not the uuid), its kind
/// and the patient's legacy key. A read-side fact about a row that already exists, not an aggregate: it carries no invariant of its own, so it has a plain constructor (DOM-9).
/// </summary>
/// <param name="Uuid">The issue's public identity.</param>
/// <param name="Id">The <c>lists.id</c> the link stores.</param>
/// <param name="Kind">Problem or allergy.</param>
/// <param name="Pid">The patient the issue is on, by legacy key.</param>
/// <param name="Title">The issue's title, if it has one.</param>
public sealed record IssueReference(Guid Uuid, long Id, IssueKind Kind, long Pid, string? Title);
