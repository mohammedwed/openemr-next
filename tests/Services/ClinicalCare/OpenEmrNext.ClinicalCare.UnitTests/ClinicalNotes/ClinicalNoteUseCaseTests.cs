using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.ClinicalNotes;

/// <summary>The clinical note use cases against in-memory fakes of every port: orchestration only, no database (DOM-19).</summary>
public class ClinicalNoteUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly Guid NoteUuid = Guid.Parse("c0c0c0c0-0000-4000-8000-000000000001");
    private static readonly DateTime Stamp = new(2026, 11, 10, 8, 0, 0);
    private static readonly ConcurrencyToken Current = ConcurrencyToken.From(Stamp);

    private static readonly ClinicalNoteCodeData Codes = new(
        [new("progress_note", "Progress Note", "LOINC:11506-3", true), new("nurse_note", "Nurse Note", "LOINC:34746-8", true), new("retired", "Retired", "", false)],
        [new("cardiology", "Cardiology", true), new("old", "Old", false)]);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeEncounters(EncounterView? view) : IEncounterReader
    {
        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct) => Task.FromResult(view is not null && view.Uuid == encounterUuid ? view : null);
        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeCodes : IClinicalNoteCodeLists
    {
        public Task<ClinicalNoteCodeData> GetCodesAsync(CancellationToken ct) => Task.FromResult(Codes);
    }

    private sealed class FakeIdentities : IClinicalNoteIdentityAllocator
    {
        public ClinicalNoteIdentity Allocate() => ClinicalNoteIdentity.Create(NoteUuid);
    }

    private sealed class FakeRegistry : IClinicalNoteRegistry
    {
        public ClinicalNote? Registered { get; private set; }
        public ClinicalNoteRecordedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }

        public Task RegisterAsync(ClinicalNote note, ClinicalNoteRecordedEvent @event, AuditEntry audit, CancellationToken ct)
        {
            (Registered, Event, Audit) = (note, @event, audit);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotes(ClinicalNoteView? view) : IClinicalNoteReader
    {
        public ClinicalNoteView? View { get; set; } = view;
        public EncounterReference? AskedAbout { get; private set; }

        public Task<ClinicalNoteView?> FindAsync(EncounterReference encounter, Guid noteUuid, CancellationToken ct)
        {
            AskedAbout = encounter;
            return Task.FromResult(View is not null && View.Uuid == noteUuid ? View : null);
        }

        public Task<ClinicalNotePage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct) =>
            Task.FromResult(new ClinicalNotePage(View is null ? [] : [View], View is null ? 0 : 1));
    }

    private sealed class FakeUpdater(FakeNotes notes) : IClinicalNoteUpdater
    {
        public UpdateOutcome Outcome { get; set; } = new(UpdateStatus.Updated, new DateTime(2026, 11, 17, 9, 30, 0));
        public ClinicalNoteUpdateRequest? Request { get; private set; }

        public Task<UpdateOutcome> UpdateAsync(ClinicalNoteUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            if (Outcome.Status == UpdateStatus.Updated && notes.View is { } v)
            {
                var f = request.Facts;
                notes.View = v with { Date = f.Date, Type = f.Type, Category = f.Category, Description = f.Description, Code = f.Code, CodeText = f.CodeText, LastUpdated = Outcome.Stamp };
            }

            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task AppendAsync(AuditEntry entry, CancellationToken ct)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private static EncounterView Encounter(long? number = 1001) => new(EncounterUuid, number, PatientUuid, 42, "AMB", null, null, null, 0, null, null, 7, null);

    private static ClinicalNoteView Stored(DateTime? stamp = null) =>
        new(NoteUuid, new DateTime(2026, 11, 10), "progress_note", "cardiology", "stable", "LOINC:11506-3", "Progress Note", "dr_who", stamp ?? Stamp);

    private static ClinicalNoteInput Same() => new(new DateTime(2026, 11, 10), "progress_note", "cardiology", "stable");

    // Record

    private static (RecordClinicalNoteUseCase UseCase, FakeRegistry Registry, FakeAuditLog Log) BuildRecord(EncounterView? encounter)
    {
        var registry = new FakeRegistry();
        var log = new FakeAuditLog();
        return (new RecordClinicalNoteUseCase(new FakeEncounters(encounter), new FakeCodes(), new FakeIdentities(), registry, log, new FixedClock(Now)), registry, log);
    }

    [Fact]
    public async Task A_note_is_registered_with_a_defaulted_date_an_event_with_ids_only_and_an_audit_row_with_no_text()
    {
        var (useCase, registry, log) = BuildRecord(Encounter());

        var note = await useCase.ExecuteAsync(
            new RecordClinicalNoteCommand(EncounterUuid, new ClinicalNoteInput(null, "progress_note", null, "secret finding"), 7, new RequestOrigin("trace-n", "192.0.2.8")), CancellationToken.None);

        Assert.Same(note, registry.Registered);
        Assert.Equal(new DateTime(2026, 11, 17), note.Facts.Date);   // no date means today
        Assert.Equal(new ClinicalNoteRecordedEvent(NoteUuid, EncounterUuid, 1001, PatientUuid, 42, Now.UtcDateTime), registry.Event);
        var audit = registry.Audit!;
        Assert.Equal(AuditActions.ClinicalNoteRecord, audit.Action);
        Assert.Equal(NoteUuid, audit.Subject);
        Assert.Equal("user:7", audit.Actor.Subject);
        Assert.Equal("trace-n", audit.CorrelationId);
        Assert.Null(audit.Details);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task An_unknown_encounter_and_one_that_cannot_take_a_note_are_refused_and_audited()
    {
        var unknown = BuildRecord(null);
        var halfFilled = BuildRecord(Encounter(number: null));
        var command = new RecordClinicalNoteCommand(EncounterUuid, Same(), 7);

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => unknown.UseCase.ExecuteAsync(command, CancellationToken.None));
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => halfFilled.UseCase.ExecuteAsync(command, CancellationToken.None));

        Assert.Null(unknown.Registry.Registered);
        Assert.Null(halfFilled.Registry.Registered);
        Assert.Equal(EncounterUuid, Assert.Single(unknown.Log.Entries).Subject);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(halfFilled.Log.Entries).Outcome);
    }

    [Fact]
    public async Task A_validation_failure_registers_nothing_and_is_audited_as_failed_without_text()
    {
        var (useCase, registry, log) = BuildRecord(Encounter());

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => useCase.ExecuteAsync(new RecordClinicalNoteCommand(EncounterUuid, Same() with { Type = "nope" }, 7), CancellationToken.None));

        Assert.Contains("Type 'nope' is not a known note type.", ex.Errors);
        Assert.Null(registry.Registered);
        Assert.Null(Assert.Single(log.Entries).Details);
    }

    // Correct

    private static (CorrectClinicalNoteUseCase UseCase, FakeNotes Notes, FakeUpdater Updater, FakeAuditLog Log) BuildCorrect(EncounterView? encounter, ClinicalNoteView? stored)
    {
        var notes = new FakeNotes(stored);
        var updater = new FakeUpdater(notes);
        var log = new FakeAuditLog();
        return (new CorrectClinicalNoteUseCase(new FakeEncounters(encounter), notes, new FakeCodes(), updater, log, new FixedClock(Now)), notes, updater, log);
    }

    private static CorrectClinicalNoteCommand Correct(ClinicalNoteInput input, IReadOnlyList<ConcurrencyToken>? offered = null) => new(EncounterUuid, NoteUuid, offered ?? [Current], input);

    [Fact]
    public async Task A_correction_is_written_with_the_version_it_was_based_on_and_names_changed_fields_only()
    {
        var (useCase, notes, updater, log) = BuildCorrect(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Correct(Same() with { Description = "stable, secret detail" }), CancellationToken.None);

        Assert.Equal(["description"], result.ChangedFields);
        Assert.Equal("stable, secret detail", result.Note.Description);
        var request = updater.Request!;
        Assert.Equal(Current, request.Expected);
        Assert.Equal((1001, 42), (request.EncounterNumber, request.PatientPid));
        Assert.Equal(["description"], request.Event.ChangedFields);
        Assert.Equal(AuditActor.Unknown, request.Audit.Actor);
        Assert.Equal("""["description"]""", request.Audit.Details);
        Assert.Equal(1001, notes.AskedAbout!.Number);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Changing_the_type_changes_the_derived_code_but_is_one_change_named_type()
    {
        var (useCase, _, updater, _) = BuildCorrect(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Correct(Same() with { Type = "nurse_note" }), CancellationToken.None);

        Assert.Equal(["type"], result.ChangedFields);
        Assert.Equal("LOINC:34746-8", updater.Request!.Facts.Code);
        Assert.Equal("Nurse Note", updater.Request.Facts.CodeText);
    }

    [Fact]
    public async Task Clearing_the_category_and_changing_the_date_are_changes()
    {
        var (useCase, _, _, _) = BuildCorrect(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Correct(Same() with { Category = null, Date = new DateTime(2026, 11, 9) }), CancellationToken.None);

        Assert.Equal(["date", "category"], result.ChangedFields);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing()
    {
        var (useCase, _, updater, log) = BuildCorrect(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Correct(Same()), CancellationToken.None);

        Assert.Empty(result.ChangedFields);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task A_stale_or_missing_version_is_refused_before_validation_and_a_row_with_no_stamp_cannot_be_corrected()
    {
        var current = new DateTime(2026, 11, 12, 10, 0, 0);
        var (useCase, _, updater, log) = BuildCorrect(Encounter(), Stored(current));
        var noStamp = BuildCorrect(Encounter(), Stored() with { LastUpdated = null });

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Correct(Same() with { Type = "nope" }), CancellationToken.None));   // also invalid
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Correct(Same(), []), CancellationToken.None));
        var none = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => noStamp.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));

        Assert.Equal(current, ex.CurrentStamp);
        Assert.Null(none.CurrentStamp);
        Assert.Null(updater.Request);
        Assert.Equal(2, log.Entries.Count);
        Assert.All(log.Entries, e => Assert.Equal(NoteUuid, e.Subject));
    }

    [Fact]
    public async Task An_unknown_note_encounter_or_an_encounter_that_cannot_own_notes_is_not_found()
    {
        var noNote = BuildCorrect(Encounter(), null);
        var noEncounter = BuildCorrect(null, Stored());
        var halfFilled = BuildCorrect(Encounter(number: null), Stored());

        await Assert.ThrowsAsync<ClinicalNoteNotFoundException>(() => noNote.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));
        await Assert.ThrowsAsync<EncounterNotFoundException>(() => noEncounter.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));
        await Assert.ThrowsAsync<ClinicalNoteNotFoundException>(() => halfFilled.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));

        Assert.Null(halfFilled.Notes.AskedAbout);
        Assert.All(new[] { noNote.Log, noEncounter.Log, halfFilled.Log }, l => Assert.Equal(AuditOutcome.Failed, Assert.Single(l.Entries).Outcome));
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_and_a_vanished_note_are_reported_as_such()
    {
        var (useCase, _, updater, log) = BuildCorrect(Encounter(), Stored());
        var winner = new DateTime(2026, 11, 17, 9, 29, 59);
        updater.Outcome = new UpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => useCase.ExecuteAsync(Correct(Same() with { Description = "changed" }), CancellationToken.None));
        updater.Outcome = new UpdateOutcome(UpdateStatus.NotFound, null);
        await Assert.ThrowsAsync<ClinicalNoteNotFoundException>(() => useCase.ExecuteAsync(Correct(Same() with { Description = "changed" }), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentStamp);
        Assert.Equal(2, log.Entries.Count);
    }

    // Reads

    [Fact]
    public async Task Get_list_and_the_lookups_go_through_the_right_ports()
    {
        var notes = new FakeNotes(Stored());

        Assert.NotNull(await new GetClinicalNoteUseCase(new FakeEncounters(Encounter()), notes).ExecuteAsync(EncounterUuid, NoteUuid, CancellationToken.None));
        Assert.Null(await new GetClinicalNoteUseCase(new FakeEncounters(Encounter()), notes).ExecuteAsync(EncounterUuid, Guid.NewGuid(), CancellationToken.None));
        Assert.Null(await new GetClinicalNoteUseCase(new FakeEncounters(Encounter(number: null)), notes).ExecuteAsync(EncounterUuid, NoteUuid, CancellationToken.None));
        await Assert.ThrowsAsync<EncounterNotFoundException>(() => new GetClinicalNoteUseCase(new FakeEncounters(null), notes).ExecuteAsync(EncounterUuid, NoteUuid, CancellationToken.None));

        var list = new ListEncounterClinicalNotesUseCase(new FakeEncounters(Encounter()), notes);
        Assert.Equal(1, (await list.ExecuteAsync(EncounterUuid, null, null, CancellationToken.None)).Total);
        Assert.Equal(0, (await new ListEncounterClinicalNotesUseCase(new FakeEncounters(Encounter(number: null)), notes).ExecuteAsync(EncounterUuid, null, null, CancellationToken.None)).Total);
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => list.ExecuteAsync(EncounterUuid, 100_000, null, CancellationToken.None));

        var lookups = new ClinicalNoteLookupsUseCase(new FakeCodes());
        Assert.Equal(["progress_note", "nurse_note"], (await lookups.TypesAsync(CancellationToken.None)).Select(t => t.Id));   // the retired one is not offered
        Assert.Equal(["cardiology"], (await lookups.CategoriesAsync(CancellationToken.None)).Select(c => c.Id));
    }
}
