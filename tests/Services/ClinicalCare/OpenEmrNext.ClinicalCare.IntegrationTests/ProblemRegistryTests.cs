using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Problems;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The problem write adapter against a real MariaDB, as the production principal (LEG-2, ADR 0070): the <c>lists</c> row legacy would write, the rows
/// that go with it, and the same atomicity and retry guarantees as the encounter and vitals registries.
/// </summary>
[Collection("ClinicalCareContainer")]
public class ProblemRegistryTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static ProblemRecordedEvent EventFor(Problem p) =>
        new(p.Uuid, p.Patient.Uuid, p.Patient.Pid, ProblemTestData.Now.ToUniversalTime());

    internal static AuditEntry AuditFor(Problem p) =>
        new(ProblemTestData.Now.ToUniversalTime(), new AuditActor($"user:{p.ProviderId}", p.ProviderId, Verified: false),
            AuditActions.ProblemRecord, p.Uuid, AuditOutcome.Success, correlationId: "corr-prob", sourceAddress: "10.7.7.7");

    private async Task RegisterAsync(Problem problem)
    {
        await using var db = fixture.CreateDbContext();
        await new EfProblemRegistry(db, new EfAuditLog(db)).RegisterAsync(problem, EventFor(problem), AuditFor(problem), CancellationToken.None);
    }

    private Task<T?> Col<T>(string column, string where = "1=1") => fixture.RootScalarAsync<T>($"SELECT {column} FROM lists WHERE {where}");

    [Fact]
    public async Task Register_writes_the_lists_row_with_legacy_shaped_values()
    {
        var problem = ProblemTestData.NewProblem(
            title: "Asthma", begin: new DateTime(2020, 5, 1), end: new DateTime(2021, 1, 2), diagnosis: "ICD10:J45.909", outcome: 1, occurrence: 4,
            referredBy: "Dr Smith", comments: "seasonal");

        await RegisterAsync(problem);

        Assert.True(problem.Id > 0);
        Assert.Equal("medical_problem", await Col<string>("type"));
        Assert.Equal("Asthma", await Col<string>("title"));
        Assert.Equal(new DateTime(2020, 5, 1), await Col<DateTime>("begdate"));
        Assert.Equal(new DateTime(2021, 1, 2), await Col<DateTime>("enddate"));
        Assert.Equal("ICD10:J45.909", await Col<string>("diagnosis"));
        Assert.Equal(1L, await Col<long>("outcome"));
        Assert.Equal(4L, await Col<long>("occurrence"));
        Assert.Equal("Dr Smith", await Col<string>("referredby"));
        Assert.Equal("seasonal", await Col<string>("comments"));
        Assert.Equal(1L, await Col<long>("activity"));
        Assert.Equal(EncounterTestData.PatientPid, await Col<long>("pid"));
        Assert.Equal(problem.Id, await Col<long>("id"));
    }

    [Fact]
    public async Task ADR_0070_the_date_column_is_when_it_was_recorded_not_the_legacy_issue_screens_month_in_minutes_value()
    {
        var problem = ProblemTestData.NewProblem(begin: new DateTime(2019, 3, 4));

        await RegisterAsync(problem);

        Assert.Equal(ProblemTestData.Now, await Col<DateTime>("`date`"));   // 09:30:00, where the issue screen's H:m:s would give 09:11:00
    }

    [Fact]
    public async Task The_acting_providers_username_and_group_are_recorded_as_legacy_records_the_session()
    {
        await RegisterAsync(ProblemTestData.NewProblem(providerId: 7));

        Assert.Equal("dr_who", await Col<string>("`user`"));
        Assert.Equal("Default", await Col<string>("groupname"));
    }

    [Fact]
    public async Task A_provider_that_does_not_exist_leaves_the_user_null_and_does_not_invent_one()
    {
        await RegisterAsync(ProblemTestData.NewProblem(providerId: 999));

        Assert.Null(await Col<string?>("`user`"));
    }

    [Fact]
    public async Task The_uuid_is_stored_and_registered_so_legacy_recognises_it()
    {
        var problem = ProblemTestData.NewProblem();

        await RegisterAsync(problem);

        var hex = problem.Uuid.ToString("N").ToUpperInvariant();
        Assert.Equal(hex, await Col<string>("HEX(uuid)"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM uuid_registry WHERE table_name = 'lists' AND HEX(uuid) = '{hex}'"));
    }

    [Fact]
    public async Task The_list_is_marked_addressed_and_a_second_problem_keeps_that_one_row()
    {
        await RegisterAsync(ProblemTestData.NewProblem());
        await RegisterAsync(ProblemTestData.NewProblem(title: "Hypertension"));

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
        Assert.Equal("medical_problem", await fixture.RootScalarAsync<string>("SELECT type FROM lists_touch"));
        Assert.Equal(EncounterTestData.PatientPid, await fixture.RootScalarAsync<long>("SELECT pid FROM lists_touch"));
        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
    }

    [Fact]
    public async Task A_touch_row_that_legacy_wrote_first_is_left_as_it_was()
    {
        await fixture.RootExecuteAsync($"INSERT INTO lists_touch (pid, type, `date`) VALUES ({EncounterTestData.PatientPid}, 'medical_problem', '2020-01-01 00:00:00')");

        await RegisterAsync(ProblemTestData.NewProblem());

        Assert.Equal(new DateTime(2020, 1, 1), await fixture.RootScalarAsync<DateTime>("SELECT `date` FROM lists_touch"));
    }

    [Fact]
    public async Task An_allergy_touch_row_is_a_different_list_and_is_not_confused_with_the_problem_one()
    {
        await fixture.RootExecuteAsync($"INSERT INTO lists_touch (pid, type, `date`) VALUES ({EncounterTestData.PatientPid}, 'allergy', NOW())");

        await RegisterAsync(ProblemTestData.NewProblem());

        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
    }

    [Fact]
    public async Task Register_writes_its_event_and_its_audit_row_in_the_same_transaction()
    {
        var problem = ProblemTestData.NewProblem();

        await RegisterAsync(problem);

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox WHERE event_type = 'ProblemRecorded'"));
        var payload = await fixture.RootScalarAsync<string>("SELECT payload FROM clinical_care_outbox");
        Assert.DoesNotContain("Asthma", payload);          // curated: no clinical content (EVT-5)
        Assert.DoesNotContain("J45", payload);
        Assert.Equal("problem.record", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal(problem.Uuid.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Null(await fixture.RootScalarAsync<string?>("SELECT details FROM clinical_care_audit"));
    }

    [Fact]
    public async Task OPS_9_registering_the_same_identity_again_writes_nothing_twice()
    {
        var uuid = Guid.CreateVersion7();

        var first = ProblemTestData.NewProblem(uuid: uuid);
        var retried = ProblemTestData.NewProblem(uuid: uuid);
        await RegisterAsync(first);
        await RegisterAsync(retried);

        Assert.Equal(first.Id, retried.Id);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM uuid_registry WHERE table_name = 'lists'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit"));
    }

    [Fact]
    public async Task QLT_12_a_failure_part_way_through_leaves_no_partial_problem()
    {
        // The uuid is already in the registry, so its primary key collides after the lists row has been written in the same transaction.
        var problem = ProblemTestData.NewProblem();
        await fixture.RootExecuteAsync($"INSERT INTO uuid_registry (uuid, table_name) VALUES (UNHEX('{problem.Uuid:N}'), 'lists')");

        await Assert.ThrowsAnyAsync<Exception>(() => RegisterAsync(problem));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0, problem.Id);
    }

    [Fact]
    public async Task SEC_9_a_problem_cannot_commit_without_its_audit_row()
    {
        var problem = ProblemTestData.NewProblem();
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfProblemRegistry(db, new ThrowingAuditLog()).RegisterAsync(problem, EventFor(problem), AuditFor(problem), CancellationToken.None));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists_touch"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM uuid_registry WHERE table_name = 'lists'"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
        Assert.Equal(0, problem.Id);
    }

    [Fact]
    public async Task Rows_of_other_issue_types_and_other_patients_are_left_alone()
    {
        await fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, pid, activity) VALUES " +
            "(UNHEX('11111111111141118111111111111111'), 'medication', 'Aspirin', 42, 1), " +
            "(UNHEX('22222222222242228222222222222222'), 'allergy', 'Penicillin', 42, 1), " +
            "(UNHEX('33333333333343338333333333333333'), 'medical_problem', 'Someone elses', 99, 1)");

        await RegisterAsync(ProblemTestData.NewProblem());

        Assert.Equal(4L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM lists"));
        Assert.Equal("Aspirin", await Col<string>("title", "type = 'medication'"));
        Assert.Equal("Penicillin", await Col<string>("title", "type = 'allergy'"));
        Assert.Equal("Someone elses", await Col<string>("title", "pid = 99"));
    }

    [Fact]
    public async Task An_allergy_that_legacy_already_touched_does_not_stop_a_problem_being_recorded_for_the_same_patient()
    {
        await fixture.RootExecuteAsync("INSERT INTO lists (uuid, type, title, pid, activity) VALUES (UNHEX('22222222222242228222222222222222'), 'allergy', 'Penicillin', 42, 1)");

        var problem = ProblemTestData.NewProblem();
        await RegisterAsync(problem);

        Assert.True(problem.Id > 0);
    }
}
