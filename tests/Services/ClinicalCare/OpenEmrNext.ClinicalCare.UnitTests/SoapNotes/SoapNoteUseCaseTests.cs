using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.SoapNotes;

/// <summary>The SOAP use cases against in-memory fakes of every port: orchestration only, no database (DOM-19).</summary>
public class SoapNoteUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

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

    private sealed class FakeRegistry : ISoapNoteRegistry
    {
        public SoapNote? Registered { get; private set; }
        public SoapNoteRecordedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }

        public Task RegisterAsync(SoapNote note, SoapNoteRecordedEvent @event, Func<long, AuditEntry> auditFor, CancellationToken ct)
        {
            (Registered, Event, Audit) = (note, @event, auditFor(555));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotes(SoapNoteView? view) : ISoapNoteReader
    {
        public SoapNoteView? View { get; set; } = view;
        public EncounterReference? AskedAbout { get; private set; }

        public Task<SoapNoteView?> FindAsync(EncounterReference encounter, long noteId, CancellationToken ct)
        {
            AskedAbout = encounter;
            return Task.FromResult(View is not null && View.Id == noteId ? View : null);
        }

        public Task<SoapNotePage> ListForEncounterAsync(EncounterReference encounter, PageRequest page, CancellationToken ct) =>
            Task.FromResult(new SoapNotePage(View is null ? [] : [View], View is null ? 0 : 1));
    }

    private sealed class FakeUpdater(FakeNotes notes) : ISoapNoteUpdater
    {
        public ContentUpdateOutcome Outcome { get; set; } = new(UpdateStatus.Updated, null);
        public SoapNoteUpdateRequest? Request { get; private set; }

        public Task<ContentUpdateOutcome> UpdateAsync(SoapNoteUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            if (Outcome.Status == UpdateStatus.Updated && notes.View is { } v)
            {
                var s = request.Sections;
                notes.View = v with { Subjective = s.Subjective, Objective = s.Objective, Assessment = s.Assessment, Plan = s.Plan };
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

    private static EncounterView Encounter(long? number = 1001, long? pid = 42) =>
        new(EncounterUuid, number, PatientUuid, pid, "AMB", null, null, null, 0, null, null, 7, null);

    private static SoapNoteView Stored() => new(77, new DateTime(2026, 11, 10, 8, 0, 0), "dr_who", "cough", "lungs clear", "viral URI", "rest");

    // Record

    private static (RecordSoapNoteUseCase UseCase, FakeRegistry Registry, FakeAuditLog Log) BuildRecord(EncounterView? encounter)
    {
        var registry = new FakeRegistry();
        var log = new FakeAuditLog();
        return (new RecordSoapNoteUseCase(new FakeEncounters(encounter), registry, log, new FixedClock(Now)), registry, log);
    }

    [Fact]
    public async Task A_note_is_registered_with_an_event_naming_sections_and_an_audit_row_keyed_by_the_new_id_with_no_text()
    {
        var (useCase, registry, log) = BuildRecord(Encounter());

        var note = await useCase.ExecuteAsync(
            new RecordSoapNoteCommand(EncounterUuid, new SoapInput("secret symptoms", Plan: "secret plan"), 7, new RequestOrigin("trace-s", "192.0.2.4")), CancellationToken.None);

        Assert.Same(note, registry.Registered);
        Assert.Equal(["subjective", "plan"], registry.Event!.SectionsWritten);
        Assert.Equal(1001, registry.Event.EncounterNumber);
        Assert.Equal(PatientUuid, registry.Event.PatientUuid);
        var audit = registry.Audit!;
        Assert.Equal(AuditActions.SoapRecord, audit.Action);
        Assert.Equal("soap:555", audit.SubjectText);
        Assert.Equal("""["subjective","plan"]""", audit.Details);
        Assert.DoesNotContain("secret", audit.Details);
        Assert.Equal("user:7", audit.Actor.Subject);
        Assert.Equal("trace-s", audit.CorrelationId);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task An_unknown_encounter_is_not_found_and_an_encounter_that_cannot_take_a_note_is_a_validation_failure_both_audited()
    {
        var unknown = BuildRecord(null);
        var halfFilled = BuildRecord(Encounter(number: null));

        await Assert.ThrowsAsync<EncounterNotFoundException>(() => unknown.UseCase.ExecuteAsync(new RecordSoapNoteCommand(EncounterUuid, new SoapInput("ok"), 7), CancellationToken.None));
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => halfFilled.UseCase.ExecuteAsync(new RecordSoapNoteCommand(EncounterUuid, new SoapInput("ok"), 7), CancellationToken.None));

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
            () => useCase.ExecuteAsync(new RecordSoapNoteCommand(EncounterUuid, new SoapInput(), 7), CancellationToken.None));

        Assert.Contains("At least one section of the note is required.", ex.Errors);
        Assert.Null(registry.Registered);
        Assert.Null(Assert.Single(log.Entries).Details);
    }

    // Correct

    private static (CorrectSoapNoteUseCase UseCase, FakeNotes Notes, FakeUpdater Updater, FakeAuditLog Log) BuildCorrect(EncounterView? encounter, SoapNoteView? stored)
    {
        var notes = new FakeNotes(stored);
        var updater = new FakeUpdater(notes);
        var log = new FakeAuditLog();
        return (new CorrectSoapNoteUseCase(new FakeEncounters(encounter), notes, updater, log, new FixedClock(Now)), notes, updater, log);
    }

    private static CorrectSoapNoteCommand Correct(SoapInput input, IReadOnlyList<ContentVersion>? offered = null, long noteId = 77) =>
        new(EncounterUuid, noteId, offered ?? [Stored().Version()], input);

    private static SoapInput Same() => new("cough", "lungs clear", "viral URI", "rest");

    [Fact]
    public async Task A_correction_is_written_with_the_version_it_was_based_on_and_names_changed_sections_only()
    {
        var (useCase, notes, updater, log) = BuildCorrect(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Correct(Same() with { Plan = "rest and review in a week" }), CancellationToken.None);

        Assert.Equal(["plan"], result.ChangedSections);
        Assert.Equal("rest and review in a week", result.Note.Plan);
        var request = updater.Request!;
        Assert.Equal(Stored().Version(), request.Expected);
        Assert.Equal((77, 1001, 42), (request.NoteId, request.EncounterNumber, request.PatientPid));
        Assert.Equal(["plan"], request.Event.ChangedSections);
        Assert.Equal("soap:77", request.Audit.SubjectText);
        Assert.Equal(AuditActor.Unknown, request.Audit.Actor);
        Assert.Equal("""["plan"]""", request.Audit.Details);
        Assert.Equal(1001, notes.AskedAbout!.Number);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Clearing_a_section_by_leaving_it_out_is_a_change_but_clearing_every_one_is_refused()
    {
        var (useCase, _, _, _) = BuildCorrect(Encounter(), Stored());

        var cleared = await useCase.ExecuteAsync(Correct(new SoapInput("cough", "lungs clear", "viral URI")), CancellationToken.None);
        var fresh = BuildCorrect(Encounter(), Stored());   // the first call changed the stored note, so a second request needs a fresh one
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => fresh.UseCase.ExecuteAsync(Correct(new SoapInput()), CancellationToken.None));

        Assert.Equal(["plan"], cleared.ChangedSections);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing()
    {
        var (useCase, _, updater, log) = BuildCorrect(Encounter(), Stored());

        var result = await useCase.ExecuteAsync(Correct(Same()), CancellationToken.None);

        Assert.Empty(result.ChangedSections);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task A_stale_or_missing_version_is_refused_before_validation_and_carries_the_current_version()
    {
        var (useCase, _, updater, log) = BuildCorrect(Encounter(), Stored());

        var ex = await Assert.ThrowsAsync<SoapNoteConflictException>(
            () => useCase.ExecuteAsync(Correct(new SoapInput(), [ContentVersion.Of(["stale"])]), CancellationToken.None));   // also invalid
        await Assert.ThrowsAsync<SoapNoteConflictException>(() => useCase.ExecuteAsync(Correct(Same(), []), CancellationToken.None));

        Assert.Equal(Stored().Version(), ex.CurrentVersion);
        Assert.Null(updater.Request);
        Assert.Equal(2, log.Entries.Count);
        Assert.All(log.Entries, e => Assert.Equal("soap:77", e.SubjectText));
    }

    [Fact]
    public async Task An_unknown_note_an_unknown_encounter_and_an_encounter_that_cannot_own_notes_are_all_not_found()
    {
        var noNote = BuildCorrect(Encounter(), null);
        var noEncounter = BuildCorrect(null, Stored());
        var halfFilled = BuildCorrect(Encounter(number: null), Stored());

        await Assert.ThrowsAsync<SoapNoteNotFoundException>(() => noNote.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));
        await Assert.ThrowsAsync<EncounterNotFoundException>(() => noEncounter.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));
        await Assert.ThrowsAsync<SoapNoteNotFoundException>(() => halfFilled.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));

        Assert.Null(halfFilled.Notes.AskedAbout);   // the reader is not even asked
        Assert.All(new[] { noNote.Log, noEncounter.Log, halfFilled.Log }, l => Assert.Equal(AuditOutcome.Failed, Assert.Single(l.Entries).Outcome));
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_and_a_vanished_note_are_reported_as_such()
    {
        var (useCase, _, updater, log) = BuildCorrect(Encounter(), Stored());
        var winner = ContentVersion.Of(["winner"]);
        updater.Outcome = new ContentUpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<SoapNoteConflictException>(() => useCase.ExecuteAsync(Correct(Same() with { Plan = "changed" }), CancellationToken.None));
        updater.Outcome = new ContentUpdateOutcome(UpdateStatus.NotFound, null);
        await Assert.ThrowsAsync<SoapNoteNotFoundException>(() => useCase.ExecuteAsync(Correct(Same() with { Plan = "changed" }), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentVersion);
        Assert.Equal(2, log.Entries.Count);
    }

    // Reads

    [Fact]
    public async Task Get_and_list_go_through_the_encounter_and_a_half_filled_one_simply_has_none()
    {
        var notes = new FakeNotes(Stored());

        Assert.NotNull(await new GetSoapNoteUseCase(new FakeEncounters(Encounter()), notes).ExecuteAsync(EncounterUuid, 77, CancellationToken.None));
        Assert.Null(await new GetSoapNoteUseCase(new FakeEncounters(Encounter()), notes).ExecuteAsync(EncounterUuid, 78, CancellationToken.None));
        Assert.Null(await new GetSoapNoteUseCase(new FakeEncounters(Encounter(number: null)), notes).ExecuteAsync(EncounterUuid, 77, CancellationToken.None));
        await Assert.ThrowsAsync<EncounterNotFoundException>(() => new GetSoapNoteUseCase(new FakeEncounters(null), notes).ExecuteAsync(EncounterUuid, 77, CancellationToken.None));

        var list = new ListEncounterSoapNotesUseCase(new FakeEncounters(Encounter()), notes);
        Assert.Equal(1, (await list.ExecuteAsync(EncounterUuid, null, null, CancellationToken.None)).Total);
        Assert.Equal(0, (await new ListEncounterSoapNotesUseCase(new FakeEncounters(Encounter(number: null)), notes).ExecuteAsync(EncounterUuid, null, null, CancellationToken.None)).Total);
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => list.ExecuteAsync(EncounterUuid, 100_000, null, CancellationToken.None));
    }
}
