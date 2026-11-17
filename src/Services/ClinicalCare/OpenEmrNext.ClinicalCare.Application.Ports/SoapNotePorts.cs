using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The outbox payload for <c>SoapNoteRecorded</c>: identifiers and the names of the sections written, never their text (EVT-5). <paramref name="NoteId"/> is <c>0</c> until the registry assigns it.</summary>
public sealed record SoapNoteRecordedEvent(
    long NoteId, Guid EncounterUuid, long EncounterNumber, Guid PatientUuid, long PatientPid, IReadOnlyList<string> SectionsWritten, DateTime RecordedAtUtc)
{
    public const string EventType = "SoapNoteRecorded";
}

/// <summary>The outbox payload for <c>SoapNoteCorrected</c>: which sections changed, never their text.</summary>
public sealed record SoapNoteCorrectedEvent(
    long NoteId, Guid EncounterUuid, long EncounterNumber, Guid PatientUuid, long PatientPid, IReadOnlyList<string> ChangedSections, DateTime CorrectedAtUtc)
{
    public const string EventType = "SoapNoteCorrected";
}

/// <summary>Persists a new note with its <c>forms</c> row, event and audit row in one transaction, recording the database-assigned id on it. If it throws, nothing was written.</summary>
public interface ISoapNoteRegistry
{
    /// <param name="auditFor">Builds the success audit row from the note's id, which the database assigns only at the insert: the row names the note by that id (ADR 0073).</param>
    Task RegisterAsync(SoapNote note, SoapNoteRecordedEvent @event, Func<long, AuditEntry> auditFor, CancellationToken ct);
}

/// <summary>Reads SOAP notes without touching an aggregate (ADR 0054). Every statement matches the note through its encounter's <c>forms</c> row (ADR 0073 #2).</summary>
public interface ISoapNoteReader
{
    /// <returns>The note, or <c>null</c> when this encounter has none with that id (or legacy has deleted it).</returns>
    Task<SoapNoteView?> FindAsync(EncounterReference encounter, long noteId, CancellationToken ct);

    /// <returns>One page of the encounter's non-deleted notes, newest first.</returns>
    Task<SoapNotePage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct);
}

/// <param name="Expected">The version the caller read; the adapter verifies it under a row lock (ADR 0072).</param>
public sealed record SoapNoteUpdateRequest(
    long NoteId, long EncounterNumber, long PatientPid, ContentVersion Expected, SoapSections Sections, SoapNoteCorrectedEvent Event, AuditEntry Audit);

/// <summary>Replaces a note's sections if it is still the version the caller read, committing the row, its event and its audit row together.</summary>
public interface ISoapNoteUpdater
{
    Task<ContentUpdateOutcome> UpdateAsync(SoapNoteUpdateRequest request, CancellationToken ct);
}
