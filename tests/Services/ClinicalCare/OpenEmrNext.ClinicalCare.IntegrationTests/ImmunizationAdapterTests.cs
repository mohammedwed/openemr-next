using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The immunization adapters against a real MariaDB, as the production principal (ADR 0072): the registry's one transaction, the tolerant reader, and the updater that verifies a content version under a row lock because
/// the table has no stamp of its own.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ImmunizationAdapterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = ImmunizationTestData.Now;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await ImmunizationTestData.SeedCodesAsync(fixture);
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ImmunizationRecordedEvent EventFor(Immunization i) => new(i.Uuid, i.Patient.Uuid, i.Patient.Pid, Now.ToUniversalTime());

    private static AuditEntry AuditFor(Immunization i) =>
        new(Now.ToUniversalTime(), new AuditActor("user:7", 7, false), AuditActions.ImmunizationRecord, i.Uuid, AuditOutcome.Success);

    private async Task<Immunization> RegisterAsync(Immunization? immunization = null)
    {
        immunization ??= ImmunizationTestData.New();
        await using var db = fixture.CreateDbContext();
        await new EfImmunizationRegistry(db, new EfAuditLog(db)).RegisterAsync(immunization, EventFor(immunization), AuditFor(immunization), CancellationToken.None);
        return immunization;
    }

    private async Task<ImmunizationView> ReadAsync(Guid uuid)
    {
        await using var db = fixture.CreateDbContext();
        return (await new EfImmunizationReader(db).FindAsync(uuid, CancellationToken.None))!;
    }

    private Task<T?> Col<T>(string column, string where = "1=1") => fixture.RootScalarAsync<T>($"SELECT {column} FROM immunizations WHERE {where}");

    private Task<long> Count(string table, string where = "1=1") => fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE {where}");

    // Registry

    [Fact]
    public async Task A_recorded_immunization_round_trips_every_column_and_reads_back_as_it_was_given()
    {
        var recorded = await RegisterAsync();

        Assert.True(recorded.Id > 0);
        var view = await ReadAsync(recorded.Uuid);
        var f = recorded.Facts;
        Assert.Equal(f.AdministeredDate, view.AdministeredDate);
        Assert.Equal(6, view.ImmunizationId);
        Assert.Equal("140", view.CvxCode);
        Assert.Equal("Acme", view.Manufacturer);
        Assert.Equal("L1", view.LotNumber);
        Assert.Equal(new DateTime(2027, 1, 1), view.ExpirationDate);
        Assert.Equal(0.5m, view.AmountAdministered);
        Assert.Equal("1", view.AmountUnit);
        Assert.Equal("intramuscular", view.Route);
        Assert.Equal("left_arm", view.AdministrationSite);
        Assert.Equal("Completed", view.CompletionStatus);
        Assert.Equal("00", view.InformationSource);
        Assert.Equal(new DateTime(2026, 11, 10), view.EducationDate);
        Assert.Equal(new DateTime(2026, 8, 6), view.VisDate);
        Assert.Equal("Nurse Joy", view.AdministeredBy);
        Assert.Equal(7, view.AdministeredById);
        Assert.Equal(9, view.OrderingProvider);
        Assert.Equal("R1", view.ReasonCode);
        Assert.Equal("why", view.ReasonDescription);
        Assert.Equal("arm sore", view.Note);
        Assert.False(view.EnteredInError);
        Assert.Equal(EncounterTestData.PatientUuid, view.PatientUuid);
        Assert.Equal(42, view.PatientPid);
        Assert.Equal(Now, view.RecordedAt);
    }

    [Fact]
    public async Task Recording_also_writes_what_legacy_expects_the_uuid_registry_row_the_stamp_and_who_created_it()
    {
        var recorded = await RegisterAsync();
        var where = $"uuid = {ImmunizationTestData.Hex(recorded.Uuid)}";

        Assert.Equal(1L, await Count("uuid_registry", $"{where} AND table_name = 'immunizations'"));
        Assert.Equal(7L, await Col<long>("created_by", where));
        Assert.Equal(7L, await Col<long>("updated_by", where));
        Assert.Equal(0L, await Col<long>("added_erroneously", where));
        // update_date has no default and nothing maintains it, so the service sets it (ADR 0072 #4).
        Assert.True(await Col<DateTime>("update_date", where) > new DateTime(2020, 1, 1));
    }

    [Fact]
    public async Task Recording_writes_its_event_and_audit_row_in_the_same_commit()
    {
        var recorded = await RegisterAsync();

        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ImmunizationRecorded'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.DoesNotContain("arm sore", payload);
        Assert.DoesNotContain("Acme", payload);
        Assert.Equal("immunization.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(recorded.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_registration_retried_after_a_lost_acknowledgement_writes_nothing_twice()
    {
        var recorded = await RegisterAsync();

        await RegisterAsync(ImmunizationTestData.New(uuid: recorded.Uuid));

        Assert.Equal(1L, await Count("immunizations"));
        Assert.Equal(1L, await Count("clinical_care_audit"));
        Assert.Equal(1L, await Count("uuid_registry", "table_name = 'immunizations'"));
    }

    [Fact]
    public async Task SEC_9_an_immunization_cannot_commit_without_its_audit_row()
    {
        var immunization = ImmunizationTestData.New();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EfImmunizationRegistry(db, new ThrowingAuditLog()).RegisterAsync(immunization, EventFor(immunization), AuditFor(immunization), CancellationToken.None));

        Assert.Equal(0L, await Count("immunizations"));
        Assert.Equal(0L, await Count("uuid_registry", "table_name = 'immunizations'"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    // Reader

    [Fact]
    public async Task A_row_legacy_wrote_with_empty_text_zero_ids_and_no_amount_reads_back_as_absent()
    {
        var uuid = Guid.CreateVersion7();
        await fixture.RootExecuteAsync(
            $"""
            INSERT INTO immunizations (uuid, patient_id, administered_date, immunization_id, cvx_code, manufacturer, lot_number, administered_by_id, administered_by,
                                       note, update_date, amount_administered, route, ordering_provider, encounter_id, reason_description)
            VALUES ({ImmunizationTestData.Hex(uuid)}, 42, '2026-11-10 08:00:00', 0, '', '', '  ', 0, '', '', NOW(), 0, '', 0, 0, '')
            """);

        var view = await ReadAsync(uuid);

        Assert.Null(view.ImmunizationId);
        Assert.Null(view.CvxCode);
        Assert.Null(view.Manufacturer);
        Assert.Null(view.LotNumber);
        Assert.Null(view.AdministeredById);
        Assert.Null(view.AdministeredBy);
        Assert.Null(view.Note);
        Assert.Null(view.AmountAdministered);
        Assert.Null(view.Route);
        Assert.Null(view.OrderingProvider);
        Assert.Null(view.EncounterNumber);
        Assert.Null(view.ReasonDescription);
        Assert.Null(view.ExpirationDate);
    }

    [Fact]
    public async Task A_row_with_no_uuid_is_not_addressable_and_is_left_out_of_the_list()
    {
        await RegisterAsync();
        await fixture.RootExecuteAsync("INSERT INTO immunizations (patient_id, administered_date, cvx_code, update_date) VALUES (42, '2026-11-01 08:00:00', '88', NOW())");
        await using var db = fixture.CreateDbContext();

        var page = await new EfImmunizationReader(db).ListForPatientAsync(PatientReference.Create(EncounterTestData.PatientUuid, 42), PageRequest.Create(null, null), CancellationToken.None);

        Assert.Equal(1, page.Total);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task The_amount_survives_the_four_byte_float_column()
    {
        foreach (var amount in new[] { 0.1m, 0.3m, 0.5m, 1.25m, 12345.6789m })
        {
            var input = ImmunizationTestData.Input() with { AmountAdministered = amount };
            var recorded = await RegisterAsync(ImmunizationTestData.New(input));

            var read = (await ReadAsync(recorded.Uuid)).AmountAdministered;

            // A 4-byte float keeps about seven significant digits, so the largest amount is compared at the precision the column really has.
            Assert.Equal(Math.Round(amount, amount > 1000m ? 1 : 4), Math.Round(read!.Value, amount > 1000m ? 1 : 4));
        }
    }

    [Fact]
    public async Task A_listing_is_one_patients_most_recently_administered_first_with_a_total_and_pages_do_not_overlap()
    {
        await RegisterAsync(ImmunizationTestData.New(ImmunizationTestData.Input(given: new DateTime(2026, 1, 1))));
        await RegisterAsync(ImmunizationTestData.New(ImmunizationTestData.Input(given: new DateTime(2026, 3, 1))));
        await RegisterAsync(ImmunizationTestData.New(ImmunizationTestData.Input(given: new DateTime(2026, 2, 1))));
        await fixture.SeedPatientAsync(Guid.NewGuid(), 99);
        await RegisterAsync(ImmunizationTestData.New(pid: 99, patientUuid: Guid.NewGuid()));
        await using var db = fixture.CreateDbContext();
        var reader = new EfImmunizationReader(db);
        var patient = PatientReference.Create(EncounterTestData.PatientUuid, 42);

        var all = await reader.ListForPatientAsync(patient, PageRequest.Create(null, null), CancellationToken.None);
        var first = await reader.ListForPatientAsync(patient, PageRequest.Create(2, 0), CancellationToken.None);
        var second = await reader.ListForPatientAsync(patient, PageRequest.Create(2, 2), CancellationToken.None);

        Assert.Equal(3, all.Total);
        Assert.Equal([3, 2, 1], all.Items.Select(i => i.AdministeredDate!.Value.Month));
        Assert.Equal(all.Items.Select(i => i.Uuid), first.Items.Concat(second.Items).Select(i => i.Uuid));
        Assert.Equal(3, second.Total);
    }

    [Fact]
    public async Task An_unknown_uuid_is_not_found()
    {
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfImmunizationReader(db).FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // Updater

    private ImmunizationUpdateRequest Request(Immunization recorded, ImmunizationInput? input = null, ContentVersion? expected = null, long pid = 42, ImmunizationFacts? facts = null)
    {
        var names = new[] { "note" };
        return new ImmunizationUpdateRequest(
            recorded.Uuid, pid, expected ?? ContentVersion.Of(["no version given"]),
            facts ?? ImmunizationFacts.Create(input ?? ImmunizationTestData.Input(note: "rechecked"), pid, Now, ImmunizationTestData.Codes),
            new ImmunizationCorrectedEvent(recorded.Uuid, recorded.Patient.Uuid, pid, names, Now.ToUniversalTime()),
            new AuditEntry(Now.ToUniversalTime(), AuditActor.Unknown, AuditActions.ImmunizationCorrect, recorded.Uuid, AuditOutcome.Success, "corr-ic", "10.9.9.9", names));
    }

    private async Task<ContentUpdateOutcome> UpdateAsync(ImmunizationUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfImmunizationUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private async Task<(Immunization Recorded, ContentVersion Version)> RecordedWithVersionAsync()
    {
        var recorded = await RegisterAsync();
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
        return (recorded, (await ReadAsync(recorded.Uuid)).Version());
    }

    [Fact]
    public async Task A_correction_with_the_current_version_changes_the_owned_columns_and_returns_the_new_version()
    {
        var (recorded, version) = await RecordedWithVersionAsync();
        var before = await Col<DateTime>("update_date", $"uuid = {ImmunizationTestData.Hex(recorded.Uuid)}");

        var outcome = await UpdateAsync(Request(recorded, ImmunizationTestData.Input(note: "rechecked", lot: "L2") with { EnteredInError = true }, version));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        var view = await ReadAsync(recorded.Uuid);
        Assert.Equal("rechecked", view.Note);
        Assert.Equal("L2", view.LotNumber);
        Assert.True(view.EnteredInError);
        Assert.Equal(view.Version(), outcome.Version);
        Assert.NotEqual(version, outcome.Version);
        Assert.True(await Col<DateTime>("update_date", $"uuid = {ImmunizationTestData.Hex(recorded.Uuid)}") >= before);
    }

    [Fact]
    public async Task A_correction_leaves_the_patient_the_uuid_and_who_created_it_and_when_untouched()
    {
        var (recorded, version) = await RecordedWithVersionAsync();
        const string Identity = "SELECT CONCAT(HEX(uuid), '|', patient_id, '|', create_date, '|', created_by, '|', updated_by, '|', IFNULL(external_id, '')) FROM immunizations";
        var before = await fixture.RootScalarAsync<string>(Identity);

        await UpdateAsync(Request(recorded, expected: version));

        Assert.Equal(before, await fixture.RootScalarAsync<string>(Identity));
    }

    [Fact]
    public async Task A_correction_writes_its_event_and_audit_row_naming_fields_and_no_values()
    {
        var (recorded, version) = await RecordedWithVersionAsync();

        await UpdateAsync(Request(recorded, expected: version));

        Assert.Equal(1L, await Count("clinical_care_outbox", "event_type = 'ImmunizationCorrected'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.DoesNotContain("rechecked", payload);
        Assert.Equal("immunization.correct", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal("""["note"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
        Assert.Equal("unknown", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_stale_version_is_a_conflict_that_reports_the_current_version_and_writes_nothing()
    {
        var (recorded, version) = await RecordedWithVersionAsync();

        var outcome = await UpdateAsync(Request(recorded, expected: ContentVersion.Of(["stale"])));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal(version, outcome.Version);
        Assert.Equal("arm sore", await Col<string>("note"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
        Assert.Equal(0L, await Count("clinical_care_audit"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_between_the_read_and_the_write_is_detected_and_the_other_writers_value_survives()
    {
        var (recorded, version) = await RecordedWithVersionAsync();
        // The legacy screen's REPLACE rewrites the row without moving any stamp: nothing but the content shows it happened.
        await fixture.RootExecuteAsync("UPDATE immunizations SET note = 'edited in the legacy screen'");

        var outcome = await UpdateAsync(Request(recorded, expected: version));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.NotEqual(version, outcome.Version);
        Assert.Equal("edited in the legacy screen", await Col<string>("note"));
    }

    [Fact]
    public async Task A_change_to_a_column_the_service_does_not_own_is_not_a_conflict()
    {
        var (recorded, version) = await RecordedWithVersionAsync();
        await fixture.RootExecuteAsync("UPDATE immunizations SET external_id = 'EXT-1'");

        var outcome = await UpdateAsync(Request(recorded, expected: version));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal("EXT-1", await Col<string>("external_id"));
    }

    [Fact]
    public async Task A_correction_cannot_reach_another_patients_immunization_or_an_unknown_one()
    {
        var (recorded, version) = await RecordedWithVersionAsync();

        var otherPatient = await UpdateAsync(Request(recorded, expected: version, pid: 99));
        var unknown = await UpdateAsync(Request(ImmunizationTestData.New(), expected: version));

        Assert.Equal(UpdateStatus.NotFound, otherPatient.Status);
        Assert.Equal(UpdateStatus.NotFound, unknown.Status);
        Assert.Equal("arm sore", await Col<string>("note"));
    }

    [Fact]
    public async Task A_correction_to_the_same_values_still_succeeds_so_the_caller_is_not_misled()
    {
        var (recorded, version) = await RecordedWithVersionAsync();

        var outcome = await UpdateAsync(Request(recorded, ImmunizationTestData.Input(), version));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal(version, outcome.Version);
    }

    [Fact]
    public async Task SEC_9_a_correction_cannot_commit_without_its_audit_row()
    {
        var (recorded, version) = await RecordedWithVersionAsync();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new EfImmunizationUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(recorded, expected: version), CancellationToken.None));

        Assert.Equal("arm sore", await Col<string>("note"));
        Assert.Equal(0L, await Count("clinical_care_outbox"));
    }

    [Fact]
    public async Task Two_corrections_based_on_the_same_version_cannot_both_win_because_the_check_runs_under_a_row_lock()
    {
        var (recorded, version) = await RecordedWithVersionAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            UpdateAsync(Request(recorded, ImmunizationTestData.Input(note: $"writer {i}"), version))));

        Assert.Equal(1, outcomes.Count(o => o.Status == UpdateStatus.Updated));
        Assert.Equal(5, outcomes.Count(o => o.Status == UpdateStatus.Conflict));
        Assert.Equal(1L, await Count("clinical_care_audit", "action = 'immunization.correct'"));
    }
}
