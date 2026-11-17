using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Allergies;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The problem update adapter against a real MariaDB, as the production principal (ADR 0068, ADR 0070): a conditional write of only the owned columns, the event and
/// the audit row in one transaction, and the ways it must refuse. Above all, an update of one issue type can never reach a row of another by its uuid.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ProblemUpdaterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly ConcurrencyToken Token = ConcurrencyToken.From(Stamp);

    private Problem _problem = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);

        _problem = ProblemTestData.NewProblem();
        await using var db = fixture.CreateDbContext();
        await new EfProblemRegistry(db, new EfAuditLog(db)).RegisterAsync(
            _problem, new(_problem.Uuid, _problem.Patient.Uuid, _problem.Patient.Pid, Now), ProblemRegistryTests.AuditFor(_problem), CancellationToken.None);

        // A known stamp to base the update on, and a clean slate for the audit and outbox so each test counts only its own rows.
        await fixture.RootExecuteAsync($"UPDATE lists SET modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ProblemChange Change(
        string title = "Asthma, severe", DateTime? begin = null, DateTime? end = null, string? diagnosis = "ICD10:J45.901", int outcome = 1, int occurrence = 4,
        string? referredBy = "Dr Jones", string? comments = "resolved", string? verification = "refuted") =>
        ProblemChange.Create(title, begin ?? new DateTime(2019, 1, 1), end ?? new DateTime(2022, 2, 2), diagnosis, outcome, occurrence, referredBy, comments, verification, Now, ProblemTestData.Codes);

    private ProblemUpdateRequest Request(ProblemChange? change = null, ConcurrencyToken? expected = null, Guid? uuid = null, IReadOnlyList<string>? fields = null)
    {
        var id = uuid ?? _problem.Uuid;
        var changed = fields ?? ["title", "endDate"];
        return new ProblemUpdateRequest(
            id, expected ?? Token, change ?? Change(),
            new ProblemUpdatedEvent(id, _problem.Patient.Uuid, _problem.Patient.Pid, changed, Now),
            new AuditEntry(Now, AuditActor.Unknown, AuditActions.ProblemUpdate, id, AuditOutcome.Success, "corr-pu", "10.9.9.9", changed));
    }

    private async Task<UpdateOutcome> UpdateAsync(ProblemUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfProblemUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private Task<T?> Col<T>(string column) => fixture.RootScalarAsync<T>($"SELECT {column} FROM lists");

    [Fact]
    public async Task An_update_changes_every_owned_column_and_returns_the_new_stamp()
    {
        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal("Asthma, severe", await Col<string>("title"));
        Assert.Equal(new DateTime(2019, 1, 1), await Col<DateTime>("begdate"));
        Assert.Equal(new DateTime(2022, 2, 2), await Col<DateTime>("enddate"));
        Assert.Equal("ICD10:J45.901", await Col<string>("diagnosis"));
        Assert.Equal(1L, await Col<long>("outcome"));
        Assert.Equal(4L, await Col<long>("occurrence"));
        Assert.Equal("Dr Jones", await Col<string>("referredby"));
        Assert.Equal("resolved", await Col<string>("comments"));
        Assert.Equal("refuted", await Col<string>("verification"));
        var stored = await Col<DateTime>("modifydate");
        Assert.True(stored > Stamp);
        Assert.Equal(stored, outcome.Stamp);
    }

    [Fact]
    public async Task Resolving_then_reopening_sets_and_clears_the_end_date()
    {
        await UpdateAsync(Request(change: Change(end: new DateTime(2022, 2, 2))));
        Assert.Equal(new DateTime(2022, 2, 2), await Col<DateTime>("enddate"));
        await fixture.RootExecuteAsync($"UPDATE lists SET modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");

        await UpdateAsync(Request(change: ProblemChange.Create("Asthma", new DateTime(2019, 1, 1), null, null, 0, 0, null, null, null, Now, ProblemTestData.Codes)));

        Assert.Null(await Col<DateTime?>("enddate"));
        Assert.Null(await Col<string?>("diagnosis"));
        Assert.Equal("", await Col<string>("verification"));   // NOT NULL column: none is the empty string legacy's default holds
    }

    [Fact]
    public async Task An_update_writes_its_event_and_its_audit_row_in_the_same_commit_naming_fields_only()
    {
        await UpdateAsync(Request());

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = 'ProblemUpdated'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.Contains("title", payload);
        Assert.DoesNotContain("Asthma, severe", payload);
        Assert.Equal("problem.update", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(_problem.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("""["title","endDate"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
        Assert.Equal("unknown", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
    }

    [Fact]
    public async Task Columns_this_service_does_not_own_are_left_exactly_as_they_were()
    {
        await fixture.RootExecuteAsync(
            "UPDATE lists SET udi = 'device-1', injury_part = 'knee', external_id = 'ext-9', destination = 'ward', subtype = 'eye', " +
            $"activity = 1, modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        var before = await fixture.RootScalarAsync<string>("SELECT CONCAT(HEX(uuid), '|', pid, '|', type, '|', `date`, '|', IFNULL(`user`, ''), '|', groupname) FROM lists");

        await UpdateAsync(Request());

        Assert.Equal("device-1", await Col<string>("udi"));
        Assert.Equal("knee", await Col<string>("injury_part"));
        Assert.Equal("ext-9", await Col<string>("external_id"));
        Assert.Equal("ward", await Col<string>("destination"));
        Assert.Equal("eye", await Col<string>("subtype"));
        Assert.Equal(before, await fixture.RootScalarAsync<string>("SELECT CONCAT(HEX(uuid), '|', pid, '|', type, '|', `date`, '|', IFNULL(`user`, ''), '|', groupname) FROM lists"));
    }

    [Fact]
    public async Task A_stale_stamp_is_a_conflict_that_reports_the_current_stamp_and_writes_nothing()
    {
        var outcome = await UpdateAsync(Request(expected: ConcurrencyToken.From(Stamp.AddDays(-3))));

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.Equal(Stamp, outcome.Stamp);
        Assert.Equal("Asthma", await Col<string>("title"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task LEG_10_a_legacy_edit_between_the_read_and_the_write_is_detected_and_not_overwritten()
    {
        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the issue screen'");   // the database moves modifydate

        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Conflict, outcome.Status);
        Assert.NotEqual(Stamp, outcome.Stamp);
        Assert.Equal("edited in the issue screen", await Col<string>("comments"));
        Assert.Equal("Asthma", await Col<string>("title"));
    }

    [Fact]
    public async Task A_row_that_does_not_exist_is_not_found()
    {
        var outcome = await UpdateAsync(Request(uuid: Guid.NewGuid()));

        Assert.Equal(UpdateStatus.NotFound, outcome.Status);
        Assert.Null(outcome.Stamp);
    }

    [Fact]
    public async Task ADR_0070_a_problem_update_can_never_reach_a_row_of_another_type_by_its_uuid()
    {
        var allergy = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var medication = Guid.Parse("22222222-2222-4222-8222-222222222222");
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity, modifydate) VALUES " +
            $"(UNHEX('{allergy:N}'), 'allergy', 'Penicillin', 42, 1, '{Stamp:yyyy-MM-dd HH:mm:ss}'), " +
            $"(UNHEX('{medication:N}'), 'medication', 'Aspirin', 42, 1, '{Stamp:yyyy-MM-dd HH:mm:ss}')");

        var viaAllergy = await UpdateAsync(Request(uuid: allergy));
        var viaMedication = await UpdateAsync(Request(uuid: medication));

        Assert.Equal(UpdateStatus.NotFound, viaAllergy.Status);
        Assert.Equal(UpdateStatus.NotFound, viaMedication.Status);
        Assert.Equal("Penicillin", await fixture.RootScalarAsync<string>("SELECT title FROM lists WHERE type = 'allergy'"));
        Assert.Equal("Aspirin", await fixture.RootScalarAsync<string>("SELECT title FROM lists WHERE type = 'medication'"));
    }

    [Fact]
    public async Task Writing_identical_values_still_matches_because_the_driver_counts_matched_rows_not_changed_ones()
    {
        var same = ProblemChange.Create(
            "Asthma", new DateTime(2020, 5, 1), null, "ICD10:J45.909", 0, 1, null, "seasonal", "confirmed", Now, ProblemTestData.Codes);

        var outcome = await UpdateAsync(Request(change: same));

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
    }

    [Fact]
    public async Task SEC_9_an_update_cannot_commit_without_its_audit_row()
    {
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfProblemUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(), CancellationToken.None));

        Assert.Equal("Asthma", await Col<string>("title"));
        Assert.Equal(Stamp, await Col<DateTime>("modifydate"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }
}

/// <summary>The allergy update adapter against a real MariaDB: the same guarantees as the problem one, on the allergy columns and the allergy type.</summary>
[Collection("ClinicalCareContainer")]
public class AllergyUpdaterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5);
    private static readonly ConcurrencyToken Token = ConcurrencyToken.From(Stamp);

    private Allergy _allergy = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);

        _allergy = AllergyTestData.NewAllergy();
        await using var db = fixture.CreateDbContext();
        await new EfAllergyRegistry(db, new EfAuditLog(db)).RegisterAsync(
            _allergy, new(_allergy.Uuid, _allergy.Patient.Uuid, _allergy.Patient.Pid, Now), AllergyRegistryTests.AuditFor(_allergy), CancellationToken.None);

        await fixture.RootExecuteAsync($"UPDATE lists SET modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_audit");
        await fixture.RootExecuteAsync("DELETE FROM clinical_care_outbox");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static AllergyChange Change(
        string title = "Amoxicillin", DateTime? begin = null, DateTime? end = null, string? diagnosis = "SNOMED-CT:372687004", string? reaction = "nausea",
        string? severity = "mild", string? verification = "refuted", string? comments = "no longer allergic") =>
        AllergyChange.Create(title, begin ?? new DateTime(2010, 1, 1), end ?? new DateTime(2022, 2, 2), diagnosis, reaction, severity, verification, comments, Now, AllergyTestData.Codes);

    private AllergyUpdateRequest Request(AllergyChange? change = null, ConcurrencyToken? expected = null, Guid? uuid = null, IReadOnlyList<string>? fields = null)
    {
        var id = uuid ?? _allergy.Uuid;
        var changed = fields ?? ["title", "reaction"];
        return new AllergyUpdateRequest(
            id, expected ?? Token, change ?? Change(),
            new AllergyUpdatedEvent(id, _allergy.Patient.Uuid, _allergy.Patient.Pid, changed, Now),
            new AuditEntry(Now, AuditActor.Unknown, AuditActions.AllergyUpdate, id, AuditOutcome.Success, "corr-au", "10.9.9.9", changed));
    }

    private async Task<UpdateOutcome> UpdateAsync(AllergyUpdateRequest request)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfAllergyUpdater(db, new EfAuditLog(db)).UpdateAsync(request, CancellationToken.None);
    }

    private Task<T?> Col<T>(string column) => fixture.RootScalarAsync<T>($"SELECT {column} FROM lists");

    [Fact]
    public async Task An_update_changes_every_owned_column_and_returns_the_new_stamp()
    {
        var outcome = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Updated, outcome.Status);
        Assert.Equal("Amoxicillin", await Col<string>("title"));
        Assert.Equal(new DateTime(2010, 1, 1), await Col<DateTime>("begdate"));
        Assert.Equal(new DateTime(2022, 2, 2), await Col<DateTime>("enddate"));
        Assert.Equal("SNOMED-CT:372687004", await Col<string>("diagnosis"));
        Assert.Equal("nausea", await Col<string>("reaction"));
        Assert.Equal("mild", await Col<string>("severity_al"));
        Assert.Equal("refuted", await Col<string>("verification"));
        Assert.Equal("no longer allergic", await Col<string>("comments"));
        Assert.Equal(await Col<DateTime>("modifydate"), outcome.Stamp);
    }

    [Fact]
    public async Task Clearing_the_coded_fields_respects_the_not_null_columns_empty_string_for_reaction_and_verification_and_null_for_severity()
    {
        // Built directly: the helper's defaults would turn an explicit null date back into a date.
        var cleared = AllergyChange.Create("Penicillin", null, null, null, null, null, null, null, Now, AllergyTestData.Codes);

        await UpdateAsync(Request(change: cleared));

        Assert.Equal("", await Col<string>("reaction"));
        Assert.Equal("", await Col<string>("verification"));
        Assert.Null(await Col<string?>("severity_al"));
        Assert.Null(await Col<DateTime?>("begdate"));
        Assert.Null(await Col<DateTime?>("enddate"));
    }

    [Fact]
    public async Task An_update_writes_its_event_and_its_audit_row_in_the_same_commit_naming_fields_only()
    {
        await UpdateAsync(Request());

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = 'AllergyUpdated'"));
        Assert.DoesNotContain("nausea", await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox"));
        Assert.Equal("allergy.update", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal("""["title","reaction"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
    }

    [Fact]
    public async Task Columns_this_service_does_not_own_are_left_exactly_as_they_were()
    {
        await fixture.RootExecuteAsync($"UPDATE lists SET udi = 'device-1', external_id = 'ext-9', referredby = 'Dr X', outcome = 3, modifydate = '{Stamp:yyyy-MM-dd HH:mm:ss}'");
        var identity = "SELECT CONCAT(HEX(uuid), '|', pid, '|', type, '|', `date`, '|', IFNULL(`user`, ''), '|', groupname) FROM lists";
        var before = await fixture.RootScalarAsync<string>(identity);

        await UpdateAsync(Request());

        Assert.Equal("device-1", await Col<string>("udi"));
        Assert.Equal("ext-9", await Col<string>("external_id"));
        Assert.Equal("Dr X", await Col<string>("referredby"));
        Assert.Equal(3L, await Col<long>("outcome"));   // not an allergy field here (slice doc L4)
        Assert.Equal(before, await fixture.RootScalarAsync<string>(identity));
    }

    [Fact]
    public async Task A_stale_stamp_and_a_legacy_edit_are_conflicts_and_nothing_is_overwritten()
    {
        var stale = await UpdateAsync(Request(expected: ConcurrencyToken.From(Stamp.AddDays(-3))));
        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the issue screen'");
        var raced = await UpdateAsync(Request());

        Assert.Equal(UpdateStatus.Conflict, stale.Status);
        Assert.Equal(UpdateStatus.Conflict, raced.Status);
        Assert.Equal("edited in the issue screen", await Col<string>("comments"));
        Assert.Equal("Penicillin", await Col<string>("title"));
    }

    [Fact]
    public async Task ADR_0070_an_allergy_update_can_never_reach_a_problem_by_its_uuid_and_an_unknown_one_is_not_found()
    {
        var problem = Guid.Parse("11111111-1111-4111-8111-111111111111");
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, title, pid, activity, modifydate) VALUES (UNHEX('{problem:N}'), 'medical_problem', 'Asthma', 42, 1, '{Stamp:yyyy-MM-dd HH:mm:ss}')");

        Assert.Equal(UpdateStatus.NotFound, (await UpdateAsync(Request(uuid: problem))).Status);
        Assert.Equal(UpdateStatus.NotFound, (await UpdateAsync(Request(uuid: Guid.NewGuid()))).Status);
        Assert.Equal("Asthma", await fixture.RootScalarAsync<string>("SELECT title FROM lists WHERE type = 'medical_problem'"));
    }

    [Fact]
    public async Task SEC_9_an_update_cannot_commit_without_its_audit_row()
    {
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfAllergyUpdater(db, new ThrowingAuditLog()).UpdateAsync(Request(), CancellationToken.None));

        Assert.Equal("Penicillin", await Col<string>("title"));
        Assert.Equal(Stamp, await Col<DateTime>("modifydate"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }
}
