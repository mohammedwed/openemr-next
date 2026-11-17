using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.SoapNotes;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The SOAP adapters against a real MariaDB, as the production principal (ADR 0072, 0073): the registry's one transaction, the reader and updater that reach a note only through its own encounter's <c>forms</c>
/// row, and the content version verified under a row lock because the table has no stamp.
/// </summary>
[Collection("ClinicalCareContainer")]
public class SoapNoteAdapterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly EncounterReference Encounter = EncounterReference.Create(Guid.NewGuid(), 1001, EncounterTestData.PatientUuid, 42);

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static SoapNote New(SoapInput? input = null, EncounterReference? encounter = null, long provider = 7) =>
        SoapNote.Record(encounter ?? Encounter, SoapSections.Create(input ?? new SoapInput("cough for 3 days", "lungs clear", "viral URI", "rest and fluids")), provider, Now);

    private async Task<SoapNote> RegisterAsync(SoapNote? note = null)
    {
        note ??= New();
        await using var db = fixture.CreateDbContext();
        await new EfSoapNoteRegistry(db, new EfAuditLog(db)).RegisterAsync(
            note, new SoapNoteRecordedEvent(0, note.Encounter.Uuid, note.Encounter.Number, note.Encounter.Patient.Uuid, note.Encounter.Patient.Pid, ["subjective"], Now.ToUniversalTime()),
            id => new AuditEntry(Now.ToUniversalTime(), new AuditActor("user:7", 7, false), AuditActions.SoapRecord, $"soap:{id}", AuditOutcome.Success, changedFields: ["subjective"]),
            CancellationToken.None);
        return note;
    }

    private async Task<SoapNoteView?> ReadAsync(SoapNote note, EncounterReference? encounter = null)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfSoapNoteReader(db).FindAsync(encounter ?? note.Encounter, note.Id, CancellationToken.None);
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    // Registry and reader

    [Fact]
    public async Task A_recorded_note_round_trips_its_sections_and_who_wrote_it()
    {
        var note = await RegisterAsync();

        Assert.True(note.Id > 0);
        var view = await ReadAsync(note);
        Assert.Equal("cough for 3 days", view!.Subjective);
        Assert.Equal("lungs clear", view.Objective);
        Assert.Equal("viral URI", view.Assessment);
        Assert.Equal("rest and fluids", view.Plan);
        Assert.Equal(Now, view.Date);
        Assert.Equal("dr_who", view.RecordedBy);   // legacy's REST path leaves this empty; this service records it
    }

    [Fact]
    public async Task Recording_writes_the_forms_row_legacy_links_a_note_to_its_encounter_by()
    {
        var note = await RegisterAsync();

        Assert.Equal(1L, await Count("forms", $"formdir = 'soap' AND form_id = {note.Id} AND encounter = 1001 AND pid = 42 AND form_name = 'SOAP' AND deleted = 0 AND authorized = 1"));
        Assert.Equal(1L, await Count("form_soap", $"id = {note.Id} AND pid = 42 AND authorized = 1 AND activity = 1 AND groupname = 'Default'"));
    }

    [Fact]
    public async Task Recording_writes_its_event_naming_the_new_id_and_an_audit_row_keyed_by_it_with_no_text()
    {
        var note = await RegisterAsync();

        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'SoapNoteRecorded'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.Contains($"\"noteId\":{note.Id}", payload);
        Assert.DoesNotContain("cough", payload);
        Assert.Equal($"soap:{note.Id}", await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("soap.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
    }

    [Fact]
    public async Task SEC_9_a_note_cannot_commit_without_its_audit_row()
    {
        var note = New();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfSoapNoteRegistry(db, new ThrowingAuditLog()).RegisterAsync(
            note, new SoapNoteRecordedEvent(0, Encounter.Uuid, 1001, Encounter.Patient.Uuid, 42, [], Now), _ => throw new InvalidOperationException("audit failed"), CancellationToken.None));

        Assert.Equal(0L, await Count("form_soap"));
        Assert.Equal(0L, await Count("forms"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0, note.Id);
    }

    [Fact]
    public async Task A_note_is_reached_only_through_its_own_encounter_and_patient()
    {
        var note = await RegisterAsync();

        Assert.NotNull(await ReadAsync(note));
        Assert.Null(await ReadAsync(note, EncounterReference.Create(Guid.NewGuid(), 2002, EncounterTestData.PatientUuid, 42)));   // another encounter of the patient
        Assert.Null(await ReadAsync(note, EncounterReference.Create(Guid.NewGuid(), 1001, Guid.NewGuid(), 99)));                  // the same number, another patient
    }

    [Fact]
    public async Task A_note_legacy_has_deleted_is_not_found_and_is_left_out_of_the_list()
    {
        var note = await RegisterAsync();
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'soap'");
        await using var db = fixture.CreateDbContext();

        Assert.Null(await ReadAsync(note));
        Assert.Equal(0, (await new EfSoapNoteReader(db).ListForEncounterAsync(Encounter, PageRequest.Create(null, null), CancellationToken.None)).Total);
    }

    [Fact]
    public async Task A_row_legacy_wrote_with_empty_text_and_no_user_reads_back_as_absent_and_a_duplicated_forms_row_does_not_duplicate_it()
    {
        await fixture.RootExecuteAsync("INSERT INTO form_soap (`date`, pid, subjective, objective, assessment, plan) VALUES ('2026-11-10 08:00:00', 42, '  ', '', 'sore throat', NULL)");
        var id = await fixture.RootScalarAsync<long>("SELECT id FROM form_soap");
        await fixture.RootExecuteAsync(
            $"INSERT INTO forms (`date`, encounter, form_name, form_id, pid, authorized, deleted, formdir) VALUES (NOW(), 1001, 'SOAP', {id}, 42, 1, 0, 'soap'), (NOW(), 1001, 'SOAP', {id}, 42, 1, 0, 'soap')");
        await using var db = fixture.CreateDbContext();

        var page = await new EfSoapNoteReader(db).ListForEncounterAsync(Encounter, PageRequest.Create(null, null), CancellationToken.None);

        var view = Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
        Assert.Null(view.Subjective);
        Assert.Null(view.Objective);
        Assert.Equal("sore throat", view.Assessment);
        Assert.Null(view.Plan);
        Assert.Null(view.RecordedBy);
    }

    [Fact]
    public async Task A_listing_is_one_encounters_notes_newest_first_with_a_total_and_pages_do_not_overlap()
    {
        for (var i = 1; i <= 3; i++)
            await RegisterAsync(SoapNote.Record(Encounter, SoapSections.Create(new SoapInput($"note {i}")), 7, Now.AddHours(i)));
        await RegisterAsync(SoapNote.Record(EncounterReference.Create(Guid.NewGuid(), 2002, EncounterTestData.PatientUuid, 42), SoapSections.Create(new SoapInput("elsewhere")), 7, Now));
        await using var db = fixture.CreateDbContext();
        var reader = new EfSoapNoteReader(db);

        var all = await reader.ListForEncounterAsync(Encounter, PageRequest.Create(null, null), CancellationToken.None);
        var first = await reader.ListForEncounterAsync(Encounter, PageRequest.Create(2, 0), CancellationToken.None);
        var second = await reader.ListForEncounterAsync(Encounter, PageRequest.Create(2, 2), CancellationToken.None);

        Assert.Equal(3, all.Total);
        Assert.Equal(["note 3", "note 2", "note 1"], all.Items.Select(i => i.Subjective));
        Assert.Equal(all.Items.Select(i => i.Id), first.Items.Concat(second.Items).Select(i => i.Id));
    }

    // Updater

    private SoapNoteUpdateRequest Request(SoapNote note, SoapInput? input = null, ContentVersion? expected = null, long? encounterNumber = null, long? pid = null)
    {
        var names = new[] { "plan" };
        return new SoapNoteUpdateRequest(
            note.Id, encounterNumber ?? 1001, pid ?? 42, expected ?? ContentVersion.Of(["no version given"]),
            SoapSections.Create(input ?? new SoapInput("cough for 3 days", "lungs clear", "viral URI", "rest, fluids and review")),
            new SoapNoteCorrectedEvent(note.Id, Encounter.Uuid, encounterNumber ?? 1001, Encounter.Patient.Uuid, pid ?? 42, names, Now.ToUniversalTime()),
            new AuditEntry(Now.ToUniversalTime(), AuditActor.Unknown, AuditActions.SoapCorrect, $"soap:{note.Id}", AuditOutcome.Success, changedFields: names));
    }

    private async Task<ContentUpdateOutcome> UpdateAsync(SoapNoteUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfSoapNoteUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private async Task<(SoapNote Note, ContentVersion Version)> RecordedAsync()
    {
        var note = await RegisterAsync();
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
        return (note, (await ReadAsync(note))!.Version());
    }

    [Fact]
    public async Task A_correction_with_the_current_version_changes_the_sections_and_returns_the_new_version()
    {
        var (note, version) = await RecordedAsync();

        var outcome = await UpdateAsync(Request(note, expected: version));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        var view = await ReadAsync(note);
        Assert.Equal("rest, fluids and review", view!.Plan);
        Assert.Equal(view.Version(), outcome.Version);
        Assert.NotEqual(version, outcome.Version);
    }

    [Fact]
    public async Task A_correction_leaves_when_and_by_whom_the_patient_and_the_forms_row_exactly_as_they_were_unlike_legacys_own_update()
    {
        var (note, version) = await RecordedAsync();
        const string Identity = "SELECT CONCAT(`date`, '|', pid, '|', IFNULL(`user`, ''), '|', IFNULL(groupname, ''), '|', authorized, '|', activity) FROM form_soap";
        const string Forms = "SELECT CONCAT(`date`, '|', encounter, '|', form_name, '|', form_id, '|', pid, '|', authorized, '|', deleted, '|', formdir) FROM forms";
        var (identityBefore, formsBefore) = (await fixture.RootScalarAsync<string>(Identity), await fixture.RootScalarAsync<string>(Forms));

        await UpdateAsync(Request(note, expected: version));

        Assert.Equal(identityBefore, await fixture.RootScalarAsync<string>(Identity));
        Assert.Equal(formsBefore, await fixture.RootScalarAsync<string>(Forms));
    }

    [Fact]
    public async Task A_correction_writes_its_event_and_audit_row_naming_sections_and_no_text()
    {
        var (note, version) = await RecordedAsync();

        await UpdateAsync(Request(note, expected: version));

        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'SoapNoteCorrected'"));
        Assert.DoesNotContain("review", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox"));
        Assert.Equal($"soap:{note.Id}", await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("""["plan"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
        Assert.Equal("unknown", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_that_reports_the_current_version_and_writes_nothing()
    {
        var (note, version) = await RecordedAsync();

        var outcome = await UpdateAsync(Request(note, expected: ContentVersion.Of(["stale"])));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal(version, outcome.Version);
        Assert.Equal("rest and fluids", await fixture.RootScalarAsync<string>("SELECT plan FROM form_soap"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_between_the_read_and_the_write_is_detected_though_no_stamp_exists()
    {
        var (note, version) = await RecordedAsync();
        await fixture.RootExecuteAsync("UPDATE form_soap SET assessment = 'edited in the legacy screen'");

        var outcome = await UpdateAsync(Request(note, expected: version));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal("edited in the legacy screen", await fixture.RootScalarAsync<string>("SELECT assessment FROM form_soap"));
    }

    [Fact]
    public async Task A_note_cannot_be_corrected_through_another_encounter_or_patient_the_check_legacys_own_update_lacks()
    {
        var (note, version) = await RecordedAsync();

        var otherEncounter = await UpdateAsync(Request(note, expected: version, encounterNumber: 2002));
        var otherPatient = await UpdateAsync(Request(note, expected: version, pid: 99));

        Assert.Equal(UpdateStatus.NotFound, otherEncounter.Status);
        Assert.Equal(UpdateStatus.NotFound, otherPatient.Status);
        Assert.Equal("rest and fluids", await fixture.RootScalarAsync<string>("SELECT plan FROM form_soap"));
    }

    [Fact]
    public async Task An_unknown_or_soft_deleted_note_is_not_found()
    {
        var (note, version) = await RecordedAsync();

        var unknown = await UpdateAsync(Request(note, expected: version) with { NoteId = 987_654 });
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'soap'");
        var deleted = await UpdateAsync(Request(note, expected: version));

        Assert.Equal(UpdateStatus.NotFound, unknown.Status);
        Assert.Equal(UpdateStatus.NotFound, deleted.Status);
    }

    [Fact]
    public async Task SEC_9_a_correction_cannot_commit_without_its_audit_row()
    {
        var (note, version) = await RecordedAsync();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfSoapNoteUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(note, expected: version), CancellationToken.None));

        Assert.Equal("rest and fluids", await fixture.RootScalarAsync<string>("SELECT plan FROM form_soap"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    [Fact]
    public async Task Six_concurrent_corrections_on_one_version_yield_exactly_one_winner()
    {
        var (note, version) = await RecordedAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            UpdateAsync(Request(note, new SoapInput("cough for 3 days", "lungs clear", "viral URI", $"writer {i}"), version))));

        Assert.Equal(1, outcomes.Count(o => o.Status == UpdateStatus.Updated));
        Assert.Equal(5, outcomes.Count(o => o.Status == UpdateStatus.Conflict));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'soap.correct'"));
    }
}
