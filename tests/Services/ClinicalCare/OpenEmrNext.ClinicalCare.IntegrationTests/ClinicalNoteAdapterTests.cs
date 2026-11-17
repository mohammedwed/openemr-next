using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ClinicalNotes;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The clinical note adapters against a real MariaDB, as the production principal (ADR 0068, 0074): the registry's one transaction and its lock-protected parent form id, the reader and updater that reach a note only
/// through its encounter, and the conditional update on the database's own stamp.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ClinicalNoteAdapterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = ClinicalNoteTestData.Now;
    private static readonly EncounterReference Encounter = EncounterReference.Create(Guid.NewGuid(), 1001, EncounterTestData.PatientUuid, 42);
    private static readonly ConcurrencyToken Token = ConcurrencyToken.From(ClinicalNoteTestData.Stamp);

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await ClinicalNoteTestData.SeedCodesAsync(fixture);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static AuditEntry AuditFor(ClinicalNote n) => new(Now.ToUniversalTime(), new AuditActor("user:7", 7, false), AuditActions.ClinicalNoteRecord, n.Uuid, AuditOutcome.Success);

    private async Task<ClinicalNote> RegisterAsync(ClinicalNote? note = null)
    {
        note ??= ClinicalNoteTestData.New(encounter: Encounter);
        await using var db = fixture.CreateDbContext();
        await new EfClinicalNoteRegistry(db, new EfAuditLog(db)).RegisterAsync(
            note, new ClinicalNoteRecordedEvent(note.Uuid, note.Encounter.Uuid, note.Encounter.Number, note.Encounter.Patient.Uuid, note.Encounter.Patient.Pid, Now.ToUniversalTime()),
            AuditFor(note), CancellationToken.None);
        return note;
    }

    private async Task<ClinicalNoteView?> ReadAsync(ClinicalNote note, EncounterReference? encounter = null)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfClinicalNoteReader(db).FindAsync(encounter ?? note.Encounter, note.Uuid, CancellationToken.None);
    }

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    // Registry and reader

    [Fact]
    public async Task A_recorded_note_round_trips_and_carries_the_code_and_label_derived_from_its_type()
    {
        var note = await RegisterAsync();

        Assert.True(note.Id > 0);
        var view = await ReadAsync(note);
        Assert.Equal("Patient comfortable.", view!.Description);
        Assert.Equal("progress_note", view.Type);
        Assert.Equal("cardiology", view.Category);
        Assert.Equal("LOINC:11506-3", view.Code);
        Assert.Equal("Progress Note", view.CodeText);
        Assert.Equal(new DateTime(2026, 11, 10), view.Date);
        Assert.Equal("dr_who", view.RecordedBy);
        Assert.NotNull(view.LastUpdated);
    }

    [Fact]
    public async Task Recording_writes_what_legacys_form_writes_a_parent_form_row_and_the_note_row_under_it()
    {
        var note = await RegisterAsync();
        var where = $"uuid = {ClinicalNoteTestData.Hex(note.Uuid)}";

        var formId = await fixture.RootScalarAsync<long>($"SELECT form_id FROM form_clinical_notes WHERE {where}");
        Assert.Equal(1L, await Count("forms", $"formdir = 'clinical_notes' AND form_id = {formId} AND encounter = 1001 AND pid = 42 AND form_name = 'Clinical Notes Form' AND deleted = 0 AND authorized = 1"));
        Assert.Equal(1L, await Count("form_clinical_notes", $"{where} AND pid = 42 AND encounter = '1001' AND authorized = 1 AND activity = 1 AND groupname = 'Default' AND `user` = 'dr_who'"));
        Assert.Equal(1L, await Count("uuid_registry", "table_name = 'form_clinical_notes'"));
    }

    [Fact]
    public async Task Recording_writes_its_event_and_audit_row_in_the_same_commit_with_no_text()
    {
        var note = await RegisterAsync();

        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ClinicalNoteRecorded'"));
        Assert.DoesNotContain("comfortable", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox"));
        Assert.Equal("clinical_note.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(note.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_new_parent_form_takes_the_next_form_id_after_any_a_legacy_note_already_holds()
    {
        await fixture.RootExecuteAsync("INSERT INTO form_clinical_notes (form_id, pid, encounter, activity, description, `date`) VALUES (41, 42, '555', 1, 'older', '2026-01-01')");

        var note = await RegisterAsync();

        Assert.Equal(42L, await fixture.RootScalarAsync<long>($"SELECT form_id FROM form_clinical_notes WHERE uuid = {ClinicalNoteTestData.Hex(note.Uuid)}"));
    }

    [Fact]
    public async Task Eight_notes_written_at_once_each_get_their_own_parent_form_id_because_the_allocation_is_serialised()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RegisterAsync(ClinicalNoteTestData.New(encounter: Encounter))));

        Assert.Equal(8L, await Count("form_clinical_notes"));
        Assert.Equal(8L, await fixture.RootScalarAsync<long>("SELECT COUNT(DISTINCT form_id) FROM form_clinical_notes"));
        Assert.Equal(8L, await Count("forms", "formdir = 'clinical_notes'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT MIN(form_id) FROM form_clinical_notes"));
        Assert.Equal(8L, await fixture.RootScalarAsync<long>("SELECT MAX(form_id) FROM form_clinical_notes"));
    }

    [Fact]
    public async Task The_form_id_lock_is_released_after_a_write_and_after_a_failed_one()
    {
        await RegisterAsync();
        var failing = ClinicalNoteTestData.New(encounter: Encounter);
        await using (var db = fixture.CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => new EfClinicalNoteRegistry(db, new ThrowingAuditLog()).RegisterAsync(
                failing, new ClinicalNoteRecordedEvent(failing.Uuid, Encounter.Uuid, 1001, Encounter.Patient.Uuid, 42, Now), AuditFor(failing), CancellationToken.None));

        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT IS_FREE_LOCK('{EfClinicalNoteRegistry.FormIdLock}')"));
        await RegisterAsync();   // and the next writer is not blocked
        Assert.Equal(2L, await Count("form_clinical_notes"));
    }

    [Fact]
    public async Task SEC_9_a_note_cannot_commit_without_its_audit_row_and_takes_its_parent_form_with_it()
    {
        var note = ClinicalNoteTestData.New(encounter: Encounter);
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfClinicalNoteRegistry(db, new ThrowingAuditLog()).RegisterAsync(
            note, new ClinicalNoteRecordedEvent(note.Uuid, Encounter.Uuid, 1001, Encounter.Patient.Uuid, 42, Now), AuditFor(note), CancellationToken.None));

        Assert.Equal(0L, await Count("form_clinical_notes"));
        Assert.Equal(0L, await Count("forms"));
        Assert.Equal(0L, await Count("uuid_registry", "table_name = 'form_clinical_notes'"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    [Fact]
    public async Task A_registration_retried_after_a_lost_acknowledgement_writes_nothing_twice()
    {
        var note = await RegisterAsync();

        await RegisterAsync(ClinicalNoteTestData.New(encounter: Encounter, uuid: note.Uuid));

        Assert.Equal(1L, await Count("form_clinical_notes"));
        Assert.Equal(1L, await Count("forms"));
        Assert.Equal(1L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task A_note_is_reached_only_through_its_own_encounter_and_patient()
    {
        var note = await RegisterAsync();

        Assert.NotNull(await ReadAsync(note));
        Assert.Null(await ReadAsync(note, EncounterReference.Create(Guid.NewGuid(), 2002, EncounterTestData.PatientUuid, 42)));
        Assert.Null(await ReadAsync(note, EncounterReference.Create(Guid.NewGuid(), 1001, Guid.NewGuid(), 99)));
    }

    [Fact]
    public async Task A_note_legacy_has_retired_or_whose_parent_form_it_has_deleted_is_not_found_and_left_out_of_the_list()
    {
        var retired = await RegisterAsync();
        var deleted = await RegisterAsync();
        await fixture.RootExecuteAsync($"UPDATE form_clinical_notes SET activity = 0 WHERE uuid = {ClinicalNoteTestData.Hex(retired.Uuid)}");
        await fixture.RootExecuteAsync(
            $"UPDATE forms SET deleted = 1 WHERE form_id = (SELECT form_id FROM form_clinical_notes WHERE uuid = {ClinicalNoteTestData.Hex(deleted.Uuid)}) AND formdir = 'clinical_notes'");
        await using var db = fixture.CreateDbContext();

        Assert.Null(await ReadAsync(retired));
        Assert.Null(await ReadAsync(deleted));
        Assert.Equal(0, (await new EfClinicalNoteReader(db).ListForEncounterAsync(Encounter, PageRequest.Create(null, null), CancellationToken.None)).Total);
    }

    [Fact]
    public async Task A_note_whose_own_encounter_column_disagrees_with_its_parent_forms_row_is_not_reachable()
    {
        var note = await RegisterAsync();
        await fixture.RootExecuteAsync("UPDATE form_clinical_notes SET encounter = '9999'");

        Assert.Null(await ReadAsync(note));
    }

    [Fact]
    public async Task Several_legacy_notes_grouped_under_one_parent_form_are_all_listed_once_even_if_the_parent_is_duplicated()
    {
        await fixture.RootExecuteAsync(
            """
            INSERT INTO forms (`date`, encounter, form_name, form_id, pid, authorized, deleted, formdir)
            VALUES (NOW(), 1001, 'Clinical Notes Form', 7, 42, 1, 0, 'clinical_notes'), (NOW(), 1001, 'Clinical Notes Form', 7, 42, 1, 0, 'clinical_notes')
            """);
        await fixture.RootExecuteAsync(
            $"""
            INSERT INTO form_clinical_notes (form_id, uuid, `date`, pid, encounter, activity, description, clinical_notes_type, code, codetext) VALUES
                (7, {ClinicalNoteTestData.Hex(Guid.NewGuid())}, '2026-11-01', 42, '1001', 1, 'first', 'progress_note', '', ''),
                (7, {ClinicalNoteTestData.Hex(Guid.NewGuid())}, '2026-11-02', 42, '1001', 1, '   ', NULL, NULL, NULL),
                (7, NULL, '2026-11-03', 42, '1001', 1, 'no uuid, not addressable', 'progress_note', '', '')
            """);
        await using var db = fixture.CreateDbContext();

        var page = await new EfClinicalNoteReader(db).ListForEncounterAsync(Encounter, PageRequest.Create(null, null), CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.Items[0].Description);   // the blank one, newest first, reads as absent
        Assert.Null(page.Items[0].Type);
        Assert.Equal("first", page.Items[1].Description);
    }

    [Fact]
    public async Task A_listing_is_one_encounters_notes_newest_first_with_a_total_and_pages_do_not_overlap()
    {
        for (var i = 1; i <= 3; i++)
            await RegisterAsync(ClinicalNoteTestData.New(ClinicalNoteTestData.Input($"note {i}", date: new DateTime(2026, 11, i)), Encounter));
        await RegisterAsync(ClinicalNoteTestData.New(encounter: EncounterReference.Create(Guid.NewGuid(), 2002, EncounterTestData.PatientUuid, 42)));
        await using var db = fixture.CreateDbContext();
        var reader = new EfClinicalNoteReader(db);

        var all = await reader.ListForEncounterAsync(Encounter, PageRequest.Create(null, null), CancellationToken.None);
        var first = await reader.ListForEncounterAsync(Encounter, PageRequest.Create(2, 0), CancellationToken.None);
        var second = await reader.ListForEncounterAsync(Encounter, PageRequest.Create(2, 2), CancellationToken.None);

        Assert.Equal(3, all.Total);
        Assert.Equal(["note 3", "note 2", "note 1"], all.Items.Select(i => i.Description));
        Assert.Equal(all.Items.Select(i => i.Uuid), first.Items.Concat(second.Items).Select(i => i.Uuid));
    }

    // Updater

    private ClinicalNoteUpdateRequest Request(ClinicalNote note, ClinicalNoteInput? input = null, ConcurrencyToken? expected = null, long? number = null, long? pid = null)
    {
        var names = new[] { "description" };
        return new ClinicalNoteUpdateRequest(
            note.Uuid, number ?? 1001, pid ?? 42, expected ?? Token, ClinicalNoteFacts.Create(input ?? ClinicalNoteTestData.Input("Revised: comfortable, eating."), Now, ClinicalNoteTestData.Codes),
            new ClinicalNoteCorrectedEvent(note.Uuid, Encounter.Uuid, number ?? 1001, Encounter.Patient.Uuid, pid ?? 42, names, Now.ToUniversalTime()),
            new AuditEntry(Now.ToUniversalTime(), AuditActor.Unknown, AuditActions.ClinicalNoteCorrect, note.Uuid, AuditOutcome.Success, "corr-cn", "10.9.9.9", names));
    }

    private async Task<UpdateOutcome> UpdateAsync(ClinicalNoteUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfClinicalNoteUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private async Task<ClinicalNote> RecordedWithKnownStampAsync()
    {
        var note = await RegisterAsync();
        // An explicit value for last_updated stops the database moving it, giving the correction a known version to be based on.
        await fixture.RootExecuteAsync($"UPDATE form_clinical_notes SET last_updated = '{ClinicalNoteTestData.Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
        return note;
    }

    private Task<T?> Col<T>(string column) => fixture.RootScalarAsync<T>($"SELECT {column} FROM form_clinical_notes");

    [Fact]
    public async Task A_correction_changes_the_editable_columns_including_the_derived_code_and_returns_the_new_stamp()
    {
        var note = await RecordedWithKnownStampAsync();

        var outcome = await UpdateAsync(Request(note, ClinicalNoteTestData.Input("Revised.", "nurse_note", null, new DateTime(2026, 11, 9))));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal("Revised.", await Col<string>("description"));
        Assert.Equal("nurse_note", await Col<string>("clinical_notes_type"));
        Assert.Equal("LOINC:34746-8", await Col<string>("code"));
        Assert.Equal("Nurse Note", await Col<string>("codetext"));
        Assert.Null(await Col<string?>("clinical_notes_category"));
        Assert.Equal(new DateTime(2026, 11, 9), await Col<DateTime>("`date`"));
        var stored = await Col<DateTime>("last_updated");
        Assert.True(stored > ClinicalNoteTestData.Stamp);
        Assert.Equal(stored, outcome.Stamp);
    }

    [Fact]
    public async Task A_correction_leaves_who_wrote_it_the_group_the_flags_the_parent_form_and_the_related_issues_untouched()
    {
        var note = await RecordedWithKnownStampAsync();
        await fixture.RootExecuteAsync("UPDATE form_clinical_notes SET note_related_to = 'issue:12', external_id = 'EXT', last_updated = last_updated");
        const string Identity = "SELECT CONCAT(form_id, '|', HEX(uuid), '|', IFNULL(`user`, ''), '|', IFNULL(groupname, ''), '|', authorized, '|', activity, '|', pid, '|', encounter, '|', IFNULL(note_related_to, ''), '|', IFNULL(external_id, '')) FROM form_clinical_notes";
        const string Forms = "SELECT CONCAT(`date`, '|', encounter, '|', form_name, '|', form_id, '|', pid, '|', deleted, '|', formdir) FROM forms";
        var (identity, forms) = (await fixture.RootScalarAsync<string>(Identity), await fixture.RootScalarAsync<string>(Forms));

        await UpdateAsync(Request(note));

        Assert.Equal(identity, await fixture.RootScalarAsync<string>(Identity));
        Assert.Equal(forms, await fixture.RootScalarAsync<string>(Forms));
    }

    [Fact]
    public async Task A_correction_writes_its_event_and_audit_row_naming_fields_and_no_text()
    {
        var note = await RecordedWithKnownStampAsync();

        await UpdateAsync(Request(note));

        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ClinicalNoteCorrected'"));
        Assert.DoesNotContain("Revised", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox"));
        Assert.Equal("clinical_note.correct", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal("""["description"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
        Assert.Equal("unknown", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_stale_stamp_is_a_conflict_that_reports_the_current_stamp_and_writes_nothing()
    {
        var note = await RecordedWithKnownStampAsync();

        var outcome = await UpdateAsync(Request(note, expected: ConcurrencyToken.From(ClinicalNoteTestData.Stamp.AddDays(-3))));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal(ClinicalNoteTestData.Stamp, outcome.Stamp);
        Assert.Equal("Patient comfortable.", await Col<string>("description"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_between_the_read_and_the_write_is_detected_and_not_overwritten()
    {
        var note = await RecordedWithKnownStampAsync();
        await fixture.RootExecuteAsync("UPDATE form_clinical_notes SET description = 'edited in the legacy screen'");   // the database moves last_updated

        var outcome = await UpdateAsync(Request(note));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.NotEqual(ClinicalNoteTestData.Stamp, outcome.Stamp);
        Assert.Equal("edited in the legacy screen", await Col<string>("description"));
    }

    [Fact]
    public async Task A_note_cannot_be_corrected_through_another_encounter_or_patient_or_once_retired_or_when_unknown()
    {
        var note = await RecordedWithKnownStampAsync();

        Assert.Equal(UpdateStatus.NotFound, (await UpdateAsync(Request(note, number: 2002))).Status);
        Assert.Equal(UpdateStatus.NotFound, (await UpdateAsync(Request(note, pid: 99))).Status);
        Assert.Equal(UpdateStatus.NotFound, (await UpdateAsync(Request(ClinicalNoteTestData.New(encounter: Encounter)))).Status);
        await fixture.RootExecuteAsync($"UPDATE form_clinical_notes SET activity = 0, last_updated = '{ClinicalNoteTestData.Stamp:yyyy-MM-dd HH:mm:ss}'");
        Assert.Equal(UpdateStatus.NotFound, (await UpdateAsync(Request(note))).Status);
        Assert.Equal("Patient comfortable.", await Col<string>("description"));
    }

    [Fact]
    public async Task SEC_9_a_correction_cannot_commit_without_its_audit_row()
    {
        var note = await RecordedWithKnownStampAsync();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfClinicalNoteUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(note), CancellationToken.None));

        Assert.Equal("Patient comfortable.", await Col<string>("description"));
        Assert.Equal(ClinicalNoteTestData.Stamp, await Col<DateTime>("last_updated"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }
}
