using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>The outbox payload for <c>ClinicalNoteRecorded</c>: identifiers only, no text, type or category (DOM-16, EVT-5).</summary>
public sealed record ClinicalNoteRecordedEvent(Guid NoteUuid, Guid EncounterUuid, long EncounterNumber, Guid PatientUuid, long PatientPid, DateTime RecordedAtUtc)
{
    public const string EventType = "ClinicalNoteRecorded";
}

/// <summary>The outbox payload for <c>ClinicalNoteCorrected</c>: which fields changed, never their values.</summary>
public sealed record ClinicalNoteCorrectedEvent(
    Guid NoteUuid, Guid EncounterUuid, long EncounterNumber, Guid PatientUuid, long PatientPid, IReadOnlyList<string> ChangedFields, DateTime CorrectedAtUtc)
{
    public const string EventType = "ClinicalNoteCorrected";
}

/// <summary>Hands a new clinical note its identity (DOM-7).</summary>
public interface IClinicalNoteIdentityAllocator
{
    ClinicalNoteIdentity Allocate();
}

/// <summary>Persists a new note, its parent form, its event and its audit row in one transaction, recording the database-assigned id on it. If it throws, nothing was written.</summary>
public interface IClinicalNoteRegistry
{
    Task RegisterAsync(ClinicalNote note, ClinicalNoteRecordedEvent @event, AuditEntry audit, CancellationToken ct);
}

/// <summary>Reads clinical notes without touching an aggregate (ADR 0054). Every statement matches a note through its encounter's <c>forms</c> row and only active notes (ADR 0074).</summary>
public interface IClinicalNoteReader
{
    /// <returns>The note, or <c>null</c> when this encounter has none with that uuid, or legacy has deactivated or deleted it.</returns>
    Task<ClinicalNoteView?> FindAsync(EncounterReference encounter, Guid noteUuid, CancellationToken ct);

    /// <returns>One page of the encounter's active notes, newest first.</returns>
    Task<ClinicalNotePage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct);
}

/// <summary>The configured note types and categories (legacy <c>list_options</c>), behind a port so the coupling stays in one adapter (design decision D6).</summary>
public interface IClinicalNoteCodeLists
{
    Task<ClinicalNoteCodeData> GetCodesAsync(CancellationToken ct);
}

/// <param name="Expected">The version the caller read; the update applies only if the row's stamp is still this (ADR 0068).</param>
public sealed record ClinicalNoteUpdateRequest(
    Guid NoteUuid, long EncounterNumber, long PatientPid, ConcurrencyToken Expected, ClinicalNoteFacts Facts, ClinicalNoteCorrectedEvent Event, AuditEntry Audit);

/// <summary>Replaces a note's editable fields if it is still the version the caller read, committing the row, its event and its audit row together.</summary>
public interface IClinicalNoteUpdater
{
    Task<UpdateOutcome> UpdateAsync(ClinicalNoteUpdateRequest request, CancellationToken ct);
}
