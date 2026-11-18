namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The issues of a patient that can be deleted (M5n, ADR 0080): the two kinds this service owns.</summary>
public enum DeletableIssue { Problem, Allergy }

/// <summary>
/// The outbox payload for a deletion (<c>ProblemDeleted</c> or <c>AllergyDeleted</c>): which record and whose, never what it said (DOM-16, EVT-5). A consumer that holds a copy of the record should remove it.
/// </summary>
public sealed record IssueDeletedEvent(DeletableIssue Kind, Guid IssueUuid, Guid? PatientUuid, long? PatientPid, DateTime DeletedAtUtc)
{
    /// <summary>The event type for this kind. A method, not a property, so it is not serialized into the payload twice.</summary>
    public string TypeName() => TypeNameOf(Kind);

    public static string TypeNameOf(DeletableIssue kind) => kind switch
    {
        DeletableIssue.Problem => "ProblemDeleted",
        DeletableIssue.Allergy => "AllergyDeleted",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>Everything one deletion must commit together: which issue, the versions the caller may have read, the event and the audit row.</summary>
/// <param name="OfferedVersions">The <c>ETag</c>s the caller sent in <c>If-Match</c>, compared with the version the adapter reads under a row lock (LEG-10).</param>
public sealed record IssueDeletionRequest(DeletableIssue Kind, Guid IssueUuid, IReadOnlyList<string> OfferedVersions, IssueDeletedEvent Event, AuditEntry Audit);

/// <summary>
/// Deletes a problem or an allergy if it is still the version the caller read, committing the removal, its links, the event and the audit row together (ADR 0080). Unlike a form record this removes the row, as legacy does
/// for an issue: its issue screens do not hide an inactive one, so a flag would leave the two systems disagreeing about what exists. Reaches only a row of that kind.
/// </summary>
public interface IIssueDeleter
{
    Task<DeletionOutcome> DeleteAsync(IssueDeletionRequest request, CancellationToken ct);
}
