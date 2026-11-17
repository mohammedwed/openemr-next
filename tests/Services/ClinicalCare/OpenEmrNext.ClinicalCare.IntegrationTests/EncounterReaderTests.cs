using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The read adapter against rows the way <b>other legacy writers</b> leave them: soft-deleted, half-filled, with no
/// uuid, with an orphan pid (LEG-4, DOM-9). Rule ids as in docs/clinical-care/01-create-encounter-slice.md.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EncounterReaderTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Uuid1 = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Uuid2 = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Uuid3 = Guid.Parse("33333333-3333-4333-8333-333333333333");

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static PatientReference Patient() => PatientReference.Create(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);

    private Task InsertEncounterAsync(Guid? uuid, long number, long pid, string date, string reason = "visit") =>
        fixture.RootExecuteAsync(
            $"INSERT INTO form_encounter (uuid, date, pid, encounter, reason, class_code, provider_id) VALUES " +
            $"({(uuid is null ? "NULL" : $"UNHEX('{uuid:N}')")}, '{date}', {pid}, {number}, '{reason}', 'AMB', 7)");

    private Task InsertNewPatientFormAsync(long number, long pid, bool deleted) =>
        fixture.RootExecuteAsync(
            $"INSERT INTO forms (date, encounter, form_name, form_id, pid, formdir, deleted) VALUES " +
            $"(NOW(), {number}, 'New Patient Encounter', {number}, {pid}, 'newpatient', {(deleted ? 1 : 0)})");

    [Fact]
    public async Task The_view_carries_the_rows_last_update_stamp_and_it_moves_when_any_writer_changes_the_row()
    {
        await InsertEncounterAsync(Uuid1, 1001, EncounterTestData.PatientPid, "2026-11-10 09:00:00");
        await fixture.RootExecuteAsync("UPDATE form_encounter SET last_update = '2026-01-02 03:04:05'");

        await using var db = fixture.CreateDbContext();
        var before = await new EfEncounterReader(db).FindAsync(Uuid1, CancellationToken.None);

        // Any writer changing a column bumps the stamp (ON UPDATE CURRENT_TIMESTAMP): that is what makes it a usable token.
        await fixture.RootExecuteAsync("UPDATE form_encounter SET reason = 'changed by a legacy screen'");
        var after = await new EfEncounterReader(db).FindAsync(Uuid1, CancellationToken.None);

        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), before!.LastUpdate);
        Assert.True(after!.LastUpdate > before.LastUpdate);
    }

    [Fact]
    public async Task An_encounter_is_found_by_uuid_with_its_patients_uuid()
    {
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-01 10:00:00");
        await using var db = fixture.CreateDbContext();

        var view = await new EfEncounterReader(db).FindAsync(Uuid1, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(Uuid1, view.Uuid);
        Assert.Equal(501, view.Number);
        Assert.Equal(EncounterTestData.PatientUuid, view.PatientUuid);
        Assert.Equal("AMB", view.ClassCode);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0), view.Date);
    }

    [Fact]
    public async Task An_unknown_uuid_is_not_found()
    {
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfEncounterReader(db).FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CC_9_an_encounter_whose_newpatient_form_is_flagged_deleted_is_treated_as_absent()
    {
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-01 10:00:00");
        await InsertNewPatientFormAsync(501, 42, deleted: true);
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfEncounterReader(db).FindAsync(Uuid1, CancellationToken.None));
    }

    [Fact]
    public async Task CC_9_one_deleted_newpatient_row_outvotes_a_surviving_one()
    {
        // forms allows several newpatient rows per encounter; a join would let the live one win. NOT EXISTS must not.
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-01 10:00:00");
        await InsertNewPatientFormAsync(501, 42, deleted: false);
        await InsertNewPatientFormAsync(501, 42, deleted: true);
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfEncounterReader(db).FindAsync(Uuid1, CancellationToken.None));
    }

    [Fact]
    public async Task CC_9_an_encounter_with_no_forms_row_at_all_is_still_returned_because_older_data_may_lack_one()
    {
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-01 10:00:00");
        await using var db = fixture.CreateDbContext();

        Assert.NotNull(await new EfEncounterReader(db).FindAsync(Uuid1, CancellationToken.None));
    }

    [Fact]
    public async Task DOM_9_a_half_filled_legacy_row_is_read_not_turned_into_an_error()
    {
        // What another writer might leave: no date, no class, no encounter number, a pid that matches no patient.
        await fixture.RootExecuteAsync(
            $"INSERT INTO form_encounter (uuid, pid, encounter, date, class_code) VALUES (UNHEX('{Uuid2:N}'), 9999, NULL, NULL, '')");
        await using var db = fixture.CreateDbContext();

        var view = await new EfEncounterReader(db).FindAsync(Uuid2, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Null(view.Number);
        Assert.Null(view.Date);
        Assert.Null(view.PatientUuid);
        Assert.Equal("", view.ClassCode);
    }

    [Fact]
    public async Task The_list_is_newest_first_by_date_not_by_encounter_number()
    {
        // An older visit entered late has the HIGHER number; ordering by number would put it first.
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-10 09:00:00", "recent");
        await InsertEncounterAsync(Uuid2, 900, 42, "2026-01-05 09:00:00", "old, entered late");
        await InsertEncounterAsync(Uuid3, 502, 42, "2026-11-12 09:00:00", "newest");
        await using var db = fixture.CreateDbContext();

        var page = await new EfEncounterReader(db).ListForPatientAsync(Patient(), PageRequest.Create(null, null), CancellationToken.None);

        Assert.Equal([Uuid3, Uuid1, Uuid2], page.Items.Select(i => i.Uuid));
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task The_list_only_contains_this_patients_encounters()
    {
        await fixture.SeedPatientAsync(Guid.Parse("c0c0c0c0-0000-4000-8000-000000000001"), 77);
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-10 09:00:00");
        await InsertEncounterAsync(Uuid2, 502, 77, "2026-11-11 09:00:00");
        await using var db = fixture.CreateDbContext();

        var page = await new EfEncounterReader(db).ListForPatientAsync(Patient(), PageRequest.Create(null, null), CancellationToken.None);

        Assert.Equal([Uuid1], page.Items.Select(i => i.Uuid));
    }

    [Fact]
    public async Task CC_9_deleted_encounters_are_left_out_of_both_the_page_and_the_total()
    {
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-10 09:00:00");
        await InsertEncounterAsync(Uuid2, 502, 42, "2026-11-11 09:00:00");
        await InsertNewPatientFormAsync(502, 42, deleted: true);
        await using var db = fixture.CreateDbContext();

        var page = await new EfEncounterReader(db).ListForPatientAsync(Patient(), PageRequest.Create(null, null), CancellationToken.None);

        Assert.Equal([Uuid1], page.Items.Select(i => i.Uuid));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task A_legacy_row_with_no_uuid_yet_is_left_out_of_the_list()
    {
        await InsertEncounterAsync(null, 600, 42, "2026-11-10 09:00:00");
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-09 09:00:00");
        await using var db = fixture.CreateDbContext();

        var page = await new EfEncounterReader(db).ListForPatientAsync(Patient(), PageRequest.Create(null, null), CancellationToken.None);

        Assert.Equal([Uuid1], page.Items.Select(i => i.Uuid));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task API_6_paging_returns_disjoint_pages_and_the_full_total()
    {
        await InsertEncounterAsync(Uuid1, 501, 42, "2026-11-10 09:00:00");
        await InsertEncounterAsync(Uuid2, 502, 42, "2026-11-11 09:00:00");
        await InsertEncounterAsync(Uuid3, 503, 42, "2026-11-12 09:00:00");
        await using var db = fixture.CreateDbContext();
        var reader = new EfEncounterReader(db);

        var first = await reader.ListForPatientAsync(Patient(), PageRequest.Create(2, 0), CancellationToken.None);
        var second = await reader.ListForPatientAsync(Patient(), PageRequest.Create(2, 2), CancellationToken.None);

        Assert.Equal([Uuid3, Uuid2], first.Items.Select(i => i.Uuid));
        Assert.Equal([Uuid1], second.Items.Select(i => i.Uuid));
        Assert.Equal(3, first.Total);
        Assert.Equal(3, second.Total);
    }
}
