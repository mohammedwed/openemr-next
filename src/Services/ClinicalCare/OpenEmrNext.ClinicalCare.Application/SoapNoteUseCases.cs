using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The names of the SOAP sections, as the event, the audit row and the API report them.</summary>
public static class SoapSectionNames
{
    public const string Subjective = "subjective", Objective = "objective", Assessment = "assessment", Plan = "plan";

    public static IReadOnlyList<string> Written(SoapSections s) =>
        new[] { (Subjective, s.Subjective), (Objective, s.Objective), (Assessment, s.Assessment), (Plan, s.Plan) }.Where(x => x.Item2 is not null).Select(x => x.Item1).ToList();

    /// <summary>Which sections a correction changes: one answer, shared by the event, the audit row and the decision to write.</summary>
    public static IReadOnlyList<string> Changed(SoapNoteView current, SoapSections next)
    {
        var changed = new List<string>();
        if (current.Subjective != next.Subjective) changed.Add(Subjective);
        if (current.Objective != next.Objective) changed.Add(Objective);
        if (current.Assessment != next.Assessment) changed.Add(Assessment);
        if (current.Plan != next.Plan) changed.Add(Plan);
        return changed;
    }
}

internal static class SoapEncounters
{
    /// <summary>A half-filled legacy encounter (no number or patient) cannot have notes linked to it: reading finds none, writing is a validation failure.</summary>
    public static async Task<EncounterReference?> FindAsync(IEncounterReader encounters, Guid encounterUuid, CancellationToken ct)
    {
        var view = await encounters.FindAsync(encounterUuid, ct) ?? throw new EncounterNotFoundException(encounterUuid);
        return EncounterReference.TryCreate(view.Uuid, view.Number, view.PatientUuid, view.PatientPid);
    }
}

public sealed record RecordSoapNoteCommand(Guid EncounterUuid, SoapInput Input, long ProviderId, RequestOrigin? Origin = null);

/// <summary>Records a SOAP note on an existing encounter (M5d slice 3). Thin (ARC-4): find the encounter, let the domain decide, commit with event and audit row. A refusal is audited as failed.</summary>
public sealed class RecordSoapNoteUseCase(IEncounterReader encounters, ISoapNoteRegistry registry, IAuditLog auditLog, TimeProvider clock)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    /// <exception cref="ClinicalCareValidationException">A section fails validation, or the encounter cannot take a note.</exception>
    public async Task<SoapNote> ExecuteAsync(RecordSoapNoteCommand command, CancellationToken ct)
    {
        try
        {
            return await RecordAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false), AuditActions.SoapRecord,
                    command.EncounterUuid, AuditOutcome.Failed, command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    private async Task<SoapNote> RecordAsync(RecordSoapNoteCommand command, CancellationToken ct)
    {
        var encounter = await SoapEncounters.FindAsync(encounters, command.EncounterUuid, ct)
            ?? throw new ClinicalCareValidationException(["The encounter has no encounter number or no known patient, so a note cannot be attached to it."]);

        var sections = SoapSections.Create(command.Input);
        var note = SoapNote.Record(encounter, sections, command.ProviderId, clock.GetLocalNow().DateTime);   // legacy datetime columns hold server-local time (LEG-11)

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        await registry.RegisterAsync(
            note,
            new SoapNoteRecordedEvent(0, encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid, SoapSectionNames.Written(sections), nowUtc),
            // The note's id exists only after the insert, so the registry asks for the audit row once it has it.
            id => new AuditEntry(
                nowUtc, new AuditActor($"user:{command.ProviderId}", command.ProviderId, Verified: false), AuditActions.SoapRecord, CorrectSoapNoteUseCase.Key(id),
                AuditOutcome.Success, command.Origin?.CorrelationId, command.Origin?.SourceAddress, SoapSectionNames.Written(sections)),
            ct);

        return note;
    }
}

public sealed record CorrectSoapNoteCommand(Guid EncounterUuid, long NoteId, IReadOnlyList<ContentVersion> Offered, SoapInput Input, RequestOrigin? Origin = null);

public sealed record SoapNoteCorrectionResult(SoapNoteView Note, IReadOnlyList<string> ChangedSections);

/// <summary>Corrects a SOAP note's sections (ADR 0072, 0073): read it through its own encounter, check the caller's version, validate, diff, then write under a row lock. A refusal is audited as failed.</summary>
public sealed class CorrectSoapNoteUseCase(IEncounterReader encounters, ISoapNoteReader notes, ISoapNoteUpdater updater, IAuditLog auditLog, TimeProvider clock)
{
    public async Task<SoapNoteCorrectionResult> ExecuteAsync(CorrectSoapNoteCommand command, CancellationToken ct)
    {
        try
        {
            return await CorrectAsync(command, ct);
        }
        catch (Exception ex) when (ex is EncounterNotFoundException or SoapNoteNotFoundException or SoapNoteConflictException or ClinicalCareValidationException)
        {
            await auditLog.AppendAsync(
                new AuditEntry(
                    clock.GetUtcNow().UtcDateTime, AuditActor.Unknown, AuditActions.SoapCorrect, Key(command.NoteId), AuditOutcome.Failed,
                    command.Origin?.CorrelationId, command.Origin?.SourceAddress),
                ct);
            throw;
        }
    }

    internal static string Key(long noteId) => $"soap:{noteId}";

    private async Task<SoapNoteCorrectionResult> CorrectAsync(CorrectSoapNoteCommand command, CancellationToken ct)
    {
        var encounter = await SoapEncounters.FindAsync(encounters, command.EncounterUuid, ct) ?? throw new SoapNoteNotFoundException(command.NoteId);
        var current = await notes.FindAsync(encounter, command.NoteId, ct) ?? throw new SoapNoteNotFoundException(command.NoteId);

        var currentVersion = current.Version();
        if (!command.Offered.Contains(currentVersion))
            throw new SoapNoteConflictException(command.NoteId, currentVersion);

        var sections = SoapSections.Create(command.Input);
        var changed = SoapSectionNames.Changed(current, sections);
        if (changed.Count == 0)
            return new SoapNoteCorrectionResult(current, changed);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var outcome = await updater.UpdateAsync(
            new SoapNoteUpdateRequest(
                command.NoteId, encounter.Number, encounter.Patient.Pid, currentVersion, sections,
                new SoapNoteCorrectedEvent(command.NoteId, encounter.Uuid, encounter.Number, encounter.Patient.Uuid, encounter.Patient.Pid, changed, nowUtc),
                new AuditEntry(
                    nowUtc, AuditActor.Unknown, AuditActions.SoapCorrect, Key(command.NoteId), AuditOutcome.Success, command.Origin?.CorrelationId,
                    command.Origin?.SourceAddress, changed)),
            ct);

        return outcome.Status switch
        {
            UpdateStatus.Updated => new SoapNoteCorrectionResult(
                await notes.FindAsync(encounter, command.NoteId, ct) ?? throw new SoapNoteNotFoundException(command.NoteId), changed),
            UpdateStatus.Conflict => throw new SoapNoteConflictException(command.NoteId, outcome.Version),
            _ => throw new SoapNoteNotFoundException(command.NoteId)
        };
    }
}

/// <summary>Reads one SOAP note of an encounter.</summary>
public sealed class GetSoapNoteUseCase(IEncounterReader encounters, ISoapNoteReader notes)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    public async Task<SoapNoteView?> ExecuteAsync(Guid encounterUuid, long noteId, CancellationToken ct) =>
        await SoapEncounters.FindAsync(encounters, encounterUuid, ct) is { } encounter ? await notes.FindAsync(encounter, noteId, ct) : null;
}

/// <summary>Lists an encounter's SOAP notes, newest first, one bounded page at a time (API-6).</summary>
public sealed class ListEncounterSoapNotesUseCase(IEncounterReader encounters, ISoapNoteReader notes)
{
    /// <exception cref="EncounterNotFoundException">No such encounter.</exception>
    /// <exception cref="ClinicalCareValidationException">The limit or offset is out of bounds.</exception>
    public async Task<SoapNotePage> ExecuteAsync(Guid encounterUuid, int? limit, int? offset, CancellationToken ct)
    {
        var page = PageRequest.Create(limit, offset);
        return await SoapEncounters.FindAsync(encounters, encounterUuid, ct) is { } encounter
            ? await notes.ListForEncounterAsync(encounter, page, ct)
            : new SoapNotePage([], 0);
    }
}
