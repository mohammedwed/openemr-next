using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The update adapter against a real MariaDB, as the production principal (ADR 0068): a conditional write of only the owned columns, the event
/// and the audit row in one transaction, and the four ways it must refuse (stale stamp, vanished row, soft-deleted row, failed audit).
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterUpdaterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly ConcurrencyToken Token = ConcurrencyToken.From(Stamp);

    private Encounter _encounter = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);

        _encounter = EncounterTestData.NewEncounter();
        await using var db = fixture.CreateDbContext();
        await new EfEncounterRegistry(db, new EfAuditLog(db)).RegisterAsync(
            _encounter, new(_encounter.Uuid, _encounter.Number, _encounter.Patient.Uuid, _encounter.Patient.Pid, _encounter.ClassCode, _encounter.Date, Now),
            EncounterRegistryTests.AuditFor(_encounter), CancellationToken.None);

        // A known stamp to base the update on, and a clean slate for the audit and outbox so each test counts only its own rows.
        await fixture.RootExecuteAsync($"UPDATE form_encounter SET last_update = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static EncounterChange Change(
        string classCode = "EMER", int category = 9, DateTime? date = null, DateTime? onset = null, string? reason = "Chest pain",
        int facilityId = 3, int? pos = 21, long providerId = 12) =>
        EncounterChange.Create(classCode, category, date ?? new DateTime(2026, 11, 12, 14, 0, 0), onset ?? new DateTime(2026, 11, 1), reason,
            facilityId, pos, providerId, Now, EncounterTestData.Reference);

    private EncounterUpdateRequest Request(
        EncounterChange? change = null, ConcurrencyToken? expected = null, Guid? uuid = null, IReadOnlyList<string>? fields = null)
    {
        var id = uuid ?? _encounter.Uuid;
        var changed = fields ?? ["classCode", "reason"];
        return new EncounterUpdateRequest(
            id, expected ?? Token, change ?? Change(),
            new EncounterUpdatedEvent(id, _encounter.Number, _encounter.Patient.Uuid, _encounter.Patient.Pid, changed, Now),
            new AuditEntry(Now, AuditActor.Unknown, AuditActions.EncounterUpdate, id, AuditOutcome.Success, "corr-u", "10.9.9.9", changed));
    }

    private async Task<UpdateOutcome> UpdateAsync(EncounterUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfEncounterUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private Task<T?> Col<T>(string column) => fixture.RootScalarAsync<T>($"SELECT {column} FROM form_encounter");

    [Fact]
    public async Task An_update_changes_every_owned_column_and_returns_the_new_stamp()
    {
        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal("EMER", await Col<string>("class_code"));
        Assert.Equal(9L, await Col<long>("pc_catid"));
        Assert.Equal("Chest pain", await Col<string>("reason"));
        Assert.Equal(12L, await Col<long>("provider_id"));
        Assert.Equal(21L, await Col<long>("pos_code"));
        Assert.Equal(3L, await Col<long>("facility_id"));
        Assert.Equal("Main Clinic", await Col<string>("facility"));
        Assert.Equal(new DateTime(2026, 11, 12, 14, 0, 0), await Col<DateTime>("date"));
        Assert.Equal(new DateTime(2026, 11, 1), await Col<DateTime>("onset_date"));
        // The stamp is the database's own: it moved off the one set above, and the outcome reports what is now stored.
        var stored = await Col<DateTime>("last_update");
        Assert.True(stored > Stamp);
        Assert.Equal(stored, outcome.Stamp);
    }

    [Fact]
    public async Task An_update_writes_its_event_and_its_audit_row_in_the_same_commit()
    {
        await UpdateAsync(Request());

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = 'EncounterUpdated'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.Contains("classCode", payload);
        Assert.DoesNotContain("Chest pain", payload);   // names, never values (EVT-5)
        Assert.Equal("encounter.update", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal(_encounter.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("""["classCode","reason"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
        Assert.Equal("unknown", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT actor_verified FROM clinical_care_audit"));
    }

    [Fact]
    public async Task Columns_this_service_does_not_own_are_left_exactly_as_another_writer_set_them()
    {
        await fixture.RootExecuteAsync(
            "UPDATE form_encounter SET billing_note = 'billed', in_collection = 1, last_level_billed = 2, sensitivity = 'high', " +
            "referral_source = 'gp', last_update = '2026-01-02 03:04:05'");
        var uuid = await fixture.RootScalarAsync<string>("SELECT HEX(uuid) FROM form_encounter");
        var number = await Col<long>("encounter");
        var pid = await Col<long>("pid");

        await UpdateAsync(Request());

        Assert.Equal("billed", await Col<string>("billing_note"));
        Assert.Equal(1L, await Col<long>("in_collection"));
        Assert.Equal(2L, await Col<long>("last_level_billed"));
        Assert.Equal("high", await Col<string>("sensitivity"));
        Assert.Equal("gp", await Col<string>("referral_source"));
        Assert.Equal(uuid, await fixture.RootScalarAsync<string>("SELECT HEX(uuid) FROM form_encounter"));
        Assert.Equal(number, await Col<long>("encounter"));
        Assert.Equal(pid, await Col<long>("pid"));
    }

    [Fact]
    public async Task An_update_leaves_the_forms_registry_row_untouched_as_legacys_own_update_does()
    {
        var before = await fixture.RootScalarAsync<string>("SELECT CONCAT(date, '|', form_name, '|', user, '|', deleted) FROM forms");

        await UpdateAsync(Request());

        Assert.Equal(before, await fixture.RootScalarAsync<string>("SELECT CONCAT(date, '|', form_name, '|', user, '|', deleted) FROM forms"));
    }

    [Fact]
    public async Task A_stale_stamp_is_a_conflict_that_reports_the_current_stamp_and_writes_nothing()
    {
        var older = ConcurrencyToken.From(Stamp.AddDays(-3));

        var outcome = await UpdateAsync(Request(expected: older));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal(Stamp, outcome.Stamp);
        Assert.Equal("Cough", await Col<string>("reason"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_between_the_read_and_the_write_is_detected_and_not_overwritten()
    {
        // The caller read the row at Stamp. A legacy screen then saves it: the database moves last_update by itself.
        await fixture.RootExecuteAsync("UPDATE form_encounter SET reason = 'edited in the legacy screen'");

        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.NotEqual(Stamp, outcome.Stamp);
        Assert.Equal("edited in the legacy screen", await Col<string>("reason"));   // the other writer's change survives
        Assert.Equal("AMB", await Col<string>("class_code"));
    }

    [Fact]
    public async Task A_row_that_does_not_exist_is_not_found()
    {
        var outcome = await UpdateAsync(Request(uuid: Guid.NewGuid()));

        Assert.Equal(UpdateStatus.NotFound, outcome.Status);
        Assert.Null(outcome.Stamp);
    }

    [Fact]
    public async Task An_encounter_legacy_has_soft_deleted_is_not_found_and_stays_as_it_was()
    {
        await fixture.RootExecuteAsync("UPDATE forms SET deleted = 1 WHERE formdir = 'newpatient'");
        await fixture.RootExecuteAsync($"UPDATE form_encounter SET last_update = '{Stamp:yyyy-MM-dd HH:mm:ss}'");

        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.NotFound, outcome.Status);
        Assert.Equal("Cough", await Col<string>("reason"));
    }

    [Fact]
    public async Task Writing_identical_values_still_matches_because_the_driver_counts_matched_rows_not_changed_ones()
    {
        // Pins MySqlConnector's UseAffectedRows=false. If it ever flipped, an identical-value UPDATE would report 0 and read as a conflict.
        var same = Change(
            classCode: "AMB", category: 5, date: _encounter.Date, onset: null, reason: "Cough", facilityId: 3, pos: 11, providerId: 7);

        var outcome = await UpdateAsync(Request(change: same));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
    }

    [Fact]
    public async Task SEC_9_an_update_cannot_commit_without_its_audit_row()
    {
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfEncounterUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(), CancellationToken.None));

        Assert.Equal("Cough", await Col<string>("reason"));
        Assert.Equal("AMB", await Col<string>("class_code"));
        Assert.Equal(Stamp, await Col<DateTime>("last_update"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_reason_can_be_cleared_to_null()
    {
        await UpdateAsync(Request(change: Change(reason: null)));

        Assert.Null(await Col<string?>("reason"));
    }

    [Fact]
    public async Task Removing_the_facility_clears_its_name()
    {
        await UpdateAsync(Request(change: Change(facilityId: 0, pos: null)));

        Assert.Equal(0L, await Col<long>("facility_id"));
        Assert.Null(await Col<string?>("facility"));
        Assert.Null(await Col<long?>("pos_code"));
    }
}
