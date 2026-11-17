using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The names of the clinical note fields a correction can change, as the event, the audit row and the API report them.</summary>
public static class ClinicalNoteFieldNames
{
    public const string Date = "date", Type = "type", Category = "category", Description = "description";

    /// <summary>Which fields a correction changes: one answer, shared by the event, the audit row and the decision to write. The code and its label follow the type, so they are not listed separately.</summary>
    public static IReadOnlyList<string> Changed(ClinicalNoteView current, ClinicalNoteFacts next)
    {
        var changed = new List<string>();
        if (current.Date?.Date != next.Date) changed.Add(Date);
        if (current.Type != next.Type) changed.Add(Type);
        if (current.Category != next.Category) changed.Add(Category);
        if (current.Description != next.Description) changed.Add(Description);
        return changed;
    }
}

internal static class NoteEncounters
{
    public static async Task<EncounterReference?> FindAsync(IEncounterReader encounters, Guid encounterUuid, CancellationToken ct)
    {
        var view = await encounters.FindAsync(encounterUuid, ct) ?? throw new EncounterNotFoundException(encounterUuid);
        return EncounterReference.TryCreate(view.Uuid, view.Number, view.PatientUuid, view.PatientPid);
    }
}

/// <summary>Everything a caller supplies to record a clinical note. A <c>null</c> date is today.</summary>
public sealed record RecordClinicalNoteCommand(Guid EncounterUuid, ClinicalNoteInput Input, long ProviderId, RequestOrigin? Origin = null);

/// <summary>Records a clinical note on an existing encounter (M5d slice 3b). Thin (ARC-4): find the encounter, let the domain decide, commit with event and audit row. A refusal is audited as failed.</summary>
public sealed class RecordClinicalNoteUseCase(
    IEncounterReader encounters, IClinicalNoteCodeLists codeLists, IClinicalNoteIdentityAllocator identities, IClinicalNoteRegistry registry, IAuditLog auditLog, TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    /// <exception cref="ClinicalCareValidationException">A field fails validation, or the encounter cannot take a note.</exception>
    public async Task<ClinicalNote> ExecuteAsync(RecordClinicalNoteCommand command, CancellationToken ct)
    {
        try
        {
            return await RecordAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false), AuditActions.ClinicalNoteRecord,
                    command.EncounterUuid, AuditOutcome.Failed, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<ClinicalNote> RecordAsync(RecordClinicalNoteCommand command, CancellationToken ct)
    {
        var encounter = await NoteEncounters.FindAsync(encounters, command.EncounterUuid, ct)
            ?? throw new ClinicalCareValidationException(["The encounter has no encounter number or no known patient, so a note cannot be attached to it."]);

        var codes = await codeLists.GetCodesAsync(ct);
        var nowLocal = clock.GetLocalNow().DateTime;   // legacy dates hold server-local time (LEG-11)
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        var facts = ClinicalNoteFacts.Create(command.Input with { Date = command.Input.Date ?? nowLocal.Date }, nowLocal, codes);
        var note = ClinicalNote.Record(encounter, facts, command.ProviderId, identities.Allocate());

        await registry.RegisterAsync(
            note, new ClinicalNoteRecordedEvent(note.Uuid, encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid, nowUtc),
            new AuditEntry(
                nowUtc, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false), AuditActions.ClinicalNoteRecord, note.Uuid, AuditOutcome.Success,
                command.Origin?.CorrelationId, command.Origin?.SourceAddress),
            ct);

        return note;
    }
}

public sealed record CorrectClinicalNoteCommand(
    Guid EncounterUuid, Guid NoteUuid, IReadOnlyList<ConcurrencyToken> Offered, ClinicalNoteInput Input, RequestOrigin? Origin = null);

public sealed record ClinicalNoteCorrectionResult(ClinicalNoteView Note, IReadOnlyList<string> ChangedFields);

/// <summary>Corrects a clinical note's editable fields (ADR 0068): read it through its own encounter, check the caller's version, validate, diff, then write conditionally. A refusal is audited as failed.</summary>
public sealed class CorrectClinicalNoteUseCase(
    IEncounterReader encounters, IClinicalNoteReader notes, IClinicalNoteCodeLists codeLists, IClinicalNoteUpdater updater, IAuditLog auditLog, TimeProvider clock)
{
    public async Task<ClinicalNoteCorrectionResult> ExecuteAsync(CorrectClinicalNoteCommand command, CancellationToken ct)
    {
        try
        {
            return await CorrectAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or ClinicalNoteNotFoundException or ConcurrencyConflictException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, AuditActor.Unknown, AuditActions.ClinicalNoteCorrect, command.NoteUuid, AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<ClinicalNoteCorrectionResult> CorrectAsync(CorrectClinicalNoteCommand command, CancellationToken ct)
    {
        var encounter = await NoteEncounters.FindAsync(encounters, command.EncounterUuid, ct) ?? throw new ClinicalNoteNotFoundException(command.NoteUuid);
        var current = await notes.FindAsync(encounter, command.NoteUuid, ct) ?? throw new ClinicalNoteNotFoundException(command.NoteUuid);

        // The precondition comes before the request is judged; a row with no stamp cannot be verified, so it cannot be corrected.
        if (current.LastUpdated is not { } stamp || !command.Offered.Contains(ConcurrencyToken.From(stamp)))
            throw new ConcurrencyConflictException(command.NoteUuid, current.LastUpdated);

        var codes = await codeLists.GetCodesAsync(ct);
        var facts = ClinicalNoteFacts.Create(command.Input, clock.GetLocalNow().DateTime, codes);

        var changed = ClinicalNoteFieldNames.Changed(current, facts);
        if (changed.Count == 0)
            return new ClinicalNoteCorrectionResult(current, changed);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new ClinicalNoteUpdateRequest(
                command.NoteUuid, encounter.Number, encounter.Patient.Pid, ConcurrencyToken.From(stamp), facts,
                new ClinicalNoteCorrectedEvent(command.NoteUuid, encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, AuditActor.Unknown, AuditActions.ClinicalNoteCorrect, command.NoteUuid, AuditOutcome.Success, command.Origin?.CorrelationId,
                    command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            UpdateStatus.Updated => new ClinicalNoteCorrectionResult(
                await notes.FindAsync(encounter, command.NoteUuid, ct) ?? throw new ClinicalNoteNotFoundException(command.NoteUuid), changed),
            UpdateStatus.Conflict => throw new ConcurrencyConflictException(command.NoteUuid, outcome.Stamp),
            _ => throw new ClinicalNoteNotFoundException(command.NoteUuid)
        };
    }
}

/// <summary>Reads one clinical note of an encounter.</summary>
public sealed class GetClinicalNoteUseCase(IEncounterReader encounters, IClinicalNoteReader notes)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    public async Task<ClinicalNoteView?> ExecuteAsync(Guid encounterUuid, Guid noteUuid, CancellationToken ct) =>
        await NoteEncounters.FindAsync(encounters, encounterUuid, ct) is { } encounter ? await notes.FindAsync(encounter, noteUuid, ct) : null;
}

/// <summary>Lists an encounter's clinical notes, newest first, one bounded page at a time (API-6).</summary>
public sealed class ListEncounterClinicalNotesUseCase(IEncounterReader encounters, IClinicalNoteReader notes)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    /// <exception cref="ClinicalCareValidationException">The limit or offset is out of bounds.</exception>
    public async Task<ClinicalNotePage> ExecuteAsync(Guid encounterUuid, int? limit, int? offset, CancellationToken ct)
    {
        var page = PageRequest.Create(limit, offset);
        return await NoteEncounters.FindAsync(encounters, encounterUuid, ct) is { } encounter
            ? await notes.ListForEncounterAsync(encounter, page, ct)
            : new ClinicalNotePage([], 0);
    }
}

/// <summary>The note types and categories a UI can offer: the active ones, in legacy's order.</summary>
public sealed class ClinicalNoteLookupsUseCase(IClinicalNoteCodeLists codeLists)
{
    public async Task<IReadOnlyList<ClinicalNoteTypeOption>> TypesAsync(CancellationToken ct) => (await codeLists.GetCodesAsync(ct)).Types.Where(t => t.Active).ToList();

    public async Task<IReadOnlyList<ClinicalNoteCategoryOption>> CategoriesAsync(CancellationToken ct) => (await codeLists.GetCodesAsync(ct)).Categories.Where(c => c.Active).ToList();
}
