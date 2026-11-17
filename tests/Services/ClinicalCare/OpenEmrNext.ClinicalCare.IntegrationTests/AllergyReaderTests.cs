using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The allergy read adapter against rows the way <b>other legacy writers</b> leave them: other issue types in the same table, other patients, no uuid,
/// half-filled, an orphan pid, and the empty strings legacy stores in its NOT NULL columns (LEG-4, DOM-9, ADR 0070).
/// </summary>
[Collection("ClinicalCareContainer")]
public class AllergyReaderTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
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

    private async Task<AllergyView?> FindAsync(Guid uuid)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfAllergyReader(db).FindAsync(uuid, CancellationToken.None);
    }

    private async Task<AllergyPage> ListAsync(int? limit = null, int? offset = null)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfAllergyReader(db).ListForPatientAsync(Patient(), PageRequest.Create(limit, offset), CancellationToken.None);
    }

    [Fact]
    public async Task An_allergy_is_found_by_uuid_with_its_patients_uuid_and_its_coded_fields()
    {
        await fixture.RootExecuteAsync(
            $"INSERT INTO lists (uuid, type, title, pid, reaction, severity_al, verification) VALUES (UNHEX('{Uuid1:N}'), 'allergy', 'Penicillin', 42, 'hives', 'severe', 'confirmed')");

        var view = await FindAsync(Uuid1);

        Assert.NotNull(view);
        Assert.Equal("Penicillin", view.Title);
        Assert.Equal("hives", view.Reaction);
        Assert.Equal("severe", view.Severity);
        Assert.Equal("confirmed", view.Verification);
        Assert.Equal(EncounterTestData.PatientUuid, view.PatientUuid);
    }

    [Fact]
    public async Task ADR_0070_a_row_of_another_issue_type_is_not_an_allergy_even_by_its_exact_uuid()
    {
        await InsertAsync(Uuid1, "medical_problem", "Asthma", "2020-05-01 00:00:00");
        await InsertAsync(Uuid2, "medication", "Aspirin", "2020-05-01 00:00:00");
        await InsertAsync(Uuid3, "surgery", "Appendectomy", "2020-05-01 00:00:00");
        await InsertAsync(Uuid4, "health_concern", "Housing", "2020-05-01 00:00:00");

        foreach (var uuid in new[] { Uuid1, Uuid2, Uuid3, Uuid4 })
            Assert.Null(await FindAsync(uuid));
    }

    [Fact]
    public async Task ADR_0070_a_list_holds_only_allergies_of_this_patient_and_leaves_out_rows_with_no_uuid()
    {
        await InsertAsync(Uuid1, "allergy", "Penicillin", "2020-05-01 00:00:00");
        await InsertAsync(Uuid2, "medical_problem", "Asthma", "2020-05-01 00:00:00");
        await InsertAsync(Uuid3, "allergy", "Someone elses", "2020-05-01 00:00:00", pid: 99);
        await InsertAsync(null, "allergy", "Created in the issue screen", "2020-05-01 00:00:00");

        var page = await ListAsync();

        Assert.Equal(1, page.Total);
        Assert.Equal("Penicillin", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task Legacys_empty_strings_in_the_not_null_columns_read_back_as_nothing()
    {
        // The issue screen and REST leave reaction and verification as '' (NOT NULL DEFAULT ''); that is "none", not a value.
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, title, pid) VALUES (UNHEX('{Uuid1:N}'), 'allergy', 'Latex', 42)");

        var view = await FindAsync(Uuid1);

        Assert.NotNull(view);
        Assert.Null(view.Reaction);
        Assert.Null(view.Verification);
        Assert.Null(view.Severity);
        Assert.Null(view.BeginDate);
    }

    [Fact]
    public async Task A_half_filled_row_and_an_orphan_pid_are_still_read()
    {
        await fixture.RootExecuteAsync($"INSERT INTO lists (uuid, type, pid) VALUES (UNHEX('{Uuid1:N}'), 'allergy', {EncounterTestData.PatientPid})");
        await InsertAsync(Uuid2, "allergy", "Orphan", "2020-05-01 00:00:00", pid: 777);

        var bare = await FindAsync(Uuid1);
        var orphan = await FindAsync(Uuid2);

        Assert.Null(bare!.Title);
        Assert.Null(orphan!.PatientUuid);
        Assert.Equal(777, orphan.PatientPid);
        Assert.Equal(1, (await ListAsync()).Total);
    }

    [Fact]
    public async Task A_list_is_newest_first_by_begin_date_a_tie_falls_back_to_the_row_id_and_paging_never_overlaps()
    {
        await InsertAsync(Uuid1, "allergy", "Oldest", "2018-01-01 00:00:00");
        await InsertAsync(Uuid2, "allergy", "Newest", "2022-01-01 00:00:00");
        await InsertAsync(Uuid3, "allergy", "Tie, first", "2020-01-01 00:00:00");
        await InsertAsync(Uuid4, "allergy", "Tie, last", "2020-01-01 00:00:00");

        var all = (await ListAsync()).Items.Select(i => i.Title).ToList();
        var paged = (await ListAsync(limit: 2, offset: 0)).Items.Concat((await ListAsync(limit: 2, offset: 2)).Items).Select(i => i.Title).ToList();

        Assert.Equal(["Newest", "Tie, last", "Tie, first", "Oldest"], all);
        Assert.Equal(all, paged);
    }

    [Fact]
    public async Task The_last_modified_stamp_moves_when_any_writer_changes_the_row()
    {
        await InsertAsync(Uuid1, "allergy", "Penicillin", null);
        await fixture.RootExecuteAsync("UPDATE lists SET modifydate = '2026-01-02 03:04:05'");
        var before = await FindAsync(Uuid1);

        await fixture.RootExecuteAsync("UPDATE lists SET comments = 'edited in the legacy screen'");
        var after = await FindAsync(Uuid1);

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), before!.LastModified);
        Assert.True(after!.LastModified > before.LastModified);
    }

    [Fact]
    public async Task What_the_registry_wrote_reads_back_through_the_reader()
    {
        var allergy = AllergyTestData.NewAllergy(begin: new DateTime(2015, 3, 2));
        await using (var db = fixture.CreateDbContext())
            await new EfAllergyRegistry(db, new EfAuditLog(db)).RegisterAsync(
                allergy, new(allergy.Uuid, allergy.Patient.Uuid, allergy.Patient.Pid, DateTime.UtcNow), AllergyRegistryTests.AuditFor(allergy), CancellationToken.None);

        var view = await FindAsync(allergy.Uuid);

        Assert.NotNull(view);
        Assert.Equal("Penicillin", view.Title);
        Assert.Equal(new DateTime(2015, 3, 2), view.BeginDate);
        Assert.Equal("hives", view.Reaction);
        Assert.Equal("severe", view.Severity);
        Assert.Equal("confirmed", view.Verification);
        Assert.Equal("dr_who", view.RecordedBy);
    }
}
