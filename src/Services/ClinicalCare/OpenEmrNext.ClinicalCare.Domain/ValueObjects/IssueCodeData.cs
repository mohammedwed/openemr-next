namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The configured code lists an issue's coded fields are checked against, read beforehand by the use case so the domain does no I/O (DOM-7).
/// Legacy stores each code as the numeric <c>option_id</c> of a <c>list_options</c> entry (<c>outcome</c>, <c>occurrence</c>), and the column is an
/// <c>int</c>, so the sets hold the numeric ids; an entry whose id is not a whole number cannot be stored and is left out.
/// </summary>
/// <param name="Outcomes">Valid <c>lists.outcome</c> codes (list <c>outcome</c>: 0 Unassigned, 1 Resolved, 2 Improved, ...).</param>
/// <param name="Occurrences">Valid <c>lists.occurrence</c> codes (list <c>occurrence</c>: 0 Unknown, 1 First, 4 Chronic/Recurrent, ...).</param>
public sealed record IssueCodeData(IReadOnlySet<int> Outcomes, IReadOnlySet<int> Occurrences);
