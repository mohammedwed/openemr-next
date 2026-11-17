namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The records of an encounter that can be deleted (M5g, ADR 0076): the three that legacy's own screens delete by flag, not by removing the row.</summary>
public enum DeletableRecord
{
    /// <summary>Vitals: legacy's <c>forms.deleted = 1</c>.</summary>
    Vitals,

    /// <summary>A SOAP note: legacy's <c>forms.deleted = 1</c>.</summary>
    SoapNote,

    /// <summary>One clinical note: <c>form_clinical_notes.activity = 0</c>, the flag legacy's note screens already filter on.</summary>
    ClinicalNote
}

/// <summary>
/// The outbox payload for a deletion (<c>VitalsDeleted</c>, <c>SoapNoteDeleted</c> or <c>ClinicalNoteDeleted</c>): which record and where it was, never what it said (DOM-16, EVT-5). <paramref name="RecordId"/> is the
/// record's uuid, or <c>soap:{id}</c> for a SOAP note, which has no uuid (ADR 0073).
/// </summary>
public sealed record RecordDeletedEvent(
    DeletableRecord Kind, string RecordId, Guid EncounterUuid, long EncounterNumber, Guid PatientUuid, long PatientPid, DateTime DeletedAtUtc)
{
    /// <summary>The event type for this kind of record. A method, not a property, so it is not serialized into the payload twice.</summary>
    public string TypeName() => TypeNameOf(Kind);

    public static string TypeNameOf(DeletableRecord kind) => kind switch
    {
        DeletableRecord.Vitals => "VitalsDeleted",
        DeletableRecord.SoapNote => "SoapNoteDeleted",
        DeletableRecord.ClinicalNote => "ClinicalNoteDeleted",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>Everything one deletion must commit together: which record, the versions the caller may have read, the event and the audit row.</summary>
/// <param name="Key">The record's identifier in its own terms: a uuid's text for vitals and clinical notes, the form id's digits for a SOAP note.</param>
/// <param name="OfferedVersions">The <c>ETag</c>s the caller sent in <c>If-Match</c>, quoted as sent. The adapter compares them with the version it reads under a row lock (LEG-10).</param>
public sealed record RecordDeletionRequest(
    DeletableRecord Kind, string Key, long EncounterNumber, long PatientPid, IReadOnlyList<string> OfferedVersions, RecordDeletedEvent Event, AuditEntry Audit);

/// <summary>How a deletion ended. Anything but <see cref="Deleted"/> means nothing was written.</summary>
public enum DeletionStatus { Deleted, NotFound, Conflict }

/// <param name="CurrentVersion">The record's current <c>ETag</c> when the result is <see cref="DeletionStatus.Conflict"/>, so the caller can reload.</param>
public sealed record DeletionOutcome(DeletionStatus Status, string? CurrentVersion);

/// <summary>
/// Soft-deletes a record if it is still the version the caller read, committing the flag, the event and the audit row together (ADR 0076). Reaches only a record of that kind that belongs to this encounter and
/// patient and is not already deleted: a key from another encounter is not found, not deleted.
/// </summary>
public interface IRecordDeleter
{
    Task<DeletionOutcome> DeleteAsync(RecordDeletionRequest request, CancellationToken ct);
}
