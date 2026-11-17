using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The problem read adapter against rows the way <b>other legacy writers</b> leave them: other issue types in the same table, other patients, no uuid,
/// half-filled, an orphan pid (LEG-4, DOM-9, ADR 0070 #2 and #6).
/// </summary>
[Collection("ClinicalCareContainer")]
public class ProblemReaderTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Uuid1 = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Uuid2 = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Uuid3 = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Uuid4 = Guid.Parse("44444444-4444-4444-8444-444444444444");

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static PatientReference Patient() => PatientReference.Create(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);

    private Task InsertAsync(Guid? uuid, string type, string title, string? begin, long pid = EncounterTestData.PatientPid) =>
        fixture.RootExecuteAsync(
            "INSERT INTO lists (uuid, type, title, begdate, pid, activity) VALUES " +
            $"({(uuid is null ? "NULL" : $"UNHEX('{uuid:N}')")}, '{type}', '{title}', {(begin is null ? "NULL" : $"'{begin}'")}, {pid}, 1)");

    private async Task<ProblemView?> FindAsync(Guid uuid)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfProblemReader(db).FindAsync(uuid, CancellationToken.None);
    }

    private async Task<ProblemPage> ListAsync(int? limit = null, int? offset = null)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfProblemReader(db).ListForPatientAsync(Patient(), PageRequest.Create(limit, offset), CancellationToken.None);
    }

    [Fact]
    public async Task A_problem_is_found_by_uuid_with_its_patients_uuid()
    {
        await InsertAsync(Uuid1, "medical_problem", "Asthma", "2020-05-01 00:00:00");

        var view = await FindAsync(Uuid1);

        Assert.NotNull(view);
        Assert.Equal(Uuid1, view.Uuid);
        Assert.Equal("Asthma", view.Title);
        Assert.Equal(new DateTime(2020, 5, 1), view.BeginDate);
        Assert.Equal(EncounterTestData.PatientUuid, view.PatientUuid);
        Assert.Equal(EncounterTestData.PatientPid, view.PatientPid);
    }

    [Fact]
    public async Task ADR_0070_a_row_of_another_issue_type_is_not_a_problem_even_by_its_exact_uuid()
    {
        await InsertAsync(Uuid1, "allergy", "Penicillin", "2020-05-01 00:00:00");
        await InsertAsync(Uuid2, "medication", "Aspirin", "2020-05-01 00:00:00");
        await InsertAsync(Uuid3, "surgery", "Appendectomy", "2020-05-01 00:00:00");
        await InsertAsync(Uuid4, "health_concern", "Housing", "2020-05-01 00:00:00");

        Assert.Null(await FindAsync(Uuid1));
        Assert.Null(await FindAsync(Uuid2));
        Assert.Null(await FindAsync(Uuid3));
        Assert.Null(await FindAsync(Uuid4));
    }

    [Fact]
    public async Task ADR_0070_a_list_holds_only_problems_of_this_patient()
    {
        await InsertAsync(Uuid1, "medical_problem", "Asthma", "2020-05-01 00:00:00");
        await InsertAsync(Uuid2, "allergy", "Penicillin", "2020-05-01 00:00:00");
        await InsertAsync(Uuid3, "medical_problem", "Someone elses", "2020-05-01 00:00:00", pid: 99);
        await InsertAsync(Uuid4, "medication", "Aspirin", "2020-05-01 00:00:00");

        var page = await ListAsync();

        Assert.Equal(1, page.Total);
        Assert.Equal("Asthma", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task ADR_0070_a_row_with_no_uuid_is_not_addressable_and_is_left_out_of_lists_and_totals()
    {
        await InsertAsync(null, "medical_problem", "Created in the issue screen", "2020-05-01 00:00:00");
        await InsertAsync(Uuid1, "medical_problem", "Asthma", "2020-05-02 00:00:00");

        var page = await ListAsync();

        Assert.Equal(1, page.Total);
        Assert.Equal("Asthma", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task A_half_filled_legacy_row_is_still_read_not_an_error_that_hides_every_other_problem()
    {
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, pid) VALUES (UNHEX('{Uuid1:N}'), 'medical_problem', {EncounterTestData.PatientPid})");
        await InsertAsync(Uuid2, "medical_problem", "Asthma", "2020-05-01 00:00:00");

        var page = await ListAsync();

        Assert.Equal(2, page.Total);
        var bare = await FindAsync(Uuid1);
        Assert.NotNull(bare);
        Assert.Null(bare.Title);
        Assert.Null(bare.BeginDate);
        Assert.Null(bare.EndDate);
        Assert.Null(bare.Diagnosis);
        Assert.Equal(0, bare.Outcome);
        Assert.Equal(0, bare.Occurrence);
    }

    [Fact]
    public async Task A_problem_whose_pid_matches_no_known_patient_is_read_with_no_patient_uuid()
    {
        await InsertAsync(Uuid1, "medical_problem", "Orphan", "2020-05-01 00:00:00", pid: 777);

        var view = await FindAsync(Uuid1);

        Assert.NotNull(view);
        Assert.Null(view.PatientUuid);
        Assert.Equal(777, view.PatientPid);
    }

    [Fact]
    public async Task A_list_is_newest_first_by_begin_date_and_a_tie_falls_back_to_the_row_id()
    {
        await InsertAsync(Uuid1, "medical_problem", "Oldest", "2018-01-01 00:00:00");
        await InsertAsync(Uuid2, "medical_problem", "Newest", "2022-01-01 00:00:00");
        await InsertAsync(Uuid3, "medical_problem", "Tie, inserted first", "2020-01-01 00:00:00");
        await InsertAsync(Uuid4, "medical_problem", "Tie, inserted last", "2020-01-01 00:00:00");

        var titles = (await ListAsync()).Items.Select(i => i.Title).ToList();

        Assert.Equal(["Newest", "Tie, inserted last", "Tie, inserted first", "Oldest"], titles);
    }

    [Fact]
    public async Task Paging_returns_every_row_once_with_a_stable_total()
    {
        for (var i = 1; i <= 5; i++)
            await InsertAsync(Guid.NewGuid(), "medical_problem", $"P{i}", $"202{i}-01-01 00:00:00");

        var first = await ListAsync(limit: 2, offset: 0);
        var second = await ListAsync(limit: 2, offset: 2);
        var third = await ListAsync(limit: 2, offset: 4);

        var all = first.Items.Concat(second.Items).Concat(third.Items).Select(i => i.Title).ToList();
        Assert.Equal(["P5", "P4", "P3", "P2", "P1"], all);
        Assert.All(new[] { first, second, third }, p => Assert.Equal(5, p.Total));
    }

    [Fact]
    public async Task The_last_modified_stamp_is_the_rows_own_and_moves_when_any_writer_changes_it()
    {
        await InsertAsync(Uuid1, "medical_problem", "Asthma", "2020-05-01 00:00:00");
        await fixture.RootExecuteAsync("UPDATE lists SET modifydate = '2026-01-02 03:04:05'");

        var before = await FindAsync(Uuid1);
        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the legacy screen'");   // the database moves modifydate
        var after = await FindAsync(Uuid1);

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), before!.LastModified);
        Assert.True(after!.LastModified > before.LastModified);
    }

    [Fact]
    public async Task Blank_text_columns_read_back_as_nothing()
    {
        await fixture.RootExecuteAsync(
            $"INSERT INTO lists (uuid, type, title, pid, diagnosis, referredby, comments, `user`) VALUES (UNHEX('{Uuid1:N}'), 'medical_problem', 'x', 42, '  ', '', '', '')");

        var view = await FindAsync(Uuid1);

        Assert.Null(view!.Diagnosis);
        Assert.Null(view.ReferredBy);
        Assert.Null(view.Comments);
        Assert.Null(view.RecordedBy);
    }

    [Fact]
    public async Task A_patient_with_no_problems_has_an_empty_page_and_a_zero_total()
    {
        var page = await ListAsync();

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task What_the_registry_wrote_reads_back_through_the_reader()
    {
        var problem = ProblemTestData.NewProblem(title: "Asthma", end: new DateTime(2021, 1, 2), outcome: 1, occurrence: 4, referredBy: "Dr Smith");
        await using (var db = fixture.CreateDbContext())
            await new EfProblemRegistry(db, new EfAuditLog(db)).RegisterAsync(
                problem, new(problem.Uuid, problem.Patient.Uuid, problem.Patient.Pid, DateTime.UtcNow), ProblemRegistryTests.AuditFor(problem), CancellationToken.None);

        var view = await FindAsync(problem.Uuid);

        Assert.NotNull(view);
        Assert.Equal("Asthma", view.Title);
        Assert.Equal(new DateTime(2020, 5, 1), view.BeginDate);
        Assert.Equal(new DateTime(2021, 1, 2), view.EndDate);
        Assert.Equal("ICD10:J45.909", view.Diagnosis);
        Assert.Equal(1, view.Outcome);
        Assert.Equal(4, view.Occurrence);
        Assert.Equal("Dr Smith", view.ReferredBy);
        Assert.Equal("dr_who", view.RecordedBy);
        Assert.Equal(ProblemTestData.Now, view.RecordedAt);
    }
}
