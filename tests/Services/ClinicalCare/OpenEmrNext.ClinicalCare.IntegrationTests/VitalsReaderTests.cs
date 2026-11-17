using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The vitals read adapter against rows the way <b>other legacy writers</b> leave them: zeros for "not recorded",
/// free-text blood pressure, deleted forms, vitals of other encounters (LEG-4, DOM-9).
/// </summary>
[Collection("ClinicalCareContainer")]
public class VitalsReaderTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid V1 = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid V2 = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid V3 = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private static readonly EncounterReference Encounter =
        EncounterReference.Create(Guid.NewGuid(), 1001, EncounterTestData.PatientUuid, 42);

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task InsertVitalsAsync(Guid uuid, string date, string? bps = "120", string weight = "150.000000",
        long encounter = 1001, long pid = 42, bool deleted = false, string pulse = "64.000000")
    {
        await fixture.RootExecuteAsync(
            $"INSERT INTO form_vitals (uuid, date, pid, `user`, bps, bpd, weight, pulse) VALUES " +
            $"(UNHEX('{uuid:N}'), '{date}', {pid}, 'dr_who', {(bps is null ? "NULL" : $"'{bps}'")}, '80', {weight}, {pulse})");
        var id = await fixture.RootScalarAsync<long>($"SELECT id FROM form_vitals WHERE HEX(uuid) = '{uuid.ToString("N").ToUpperInvariant()}'");
        await fixture.RootExecuteAsync(
            $"INSERT INTO forms (date, encounter, form_name, form_id, pid, formdir, deleted) VALUES " +
            $"(NOW(), {encounter}, 'Vitals', {id}, {pid}, 'vitals', {(deleted ? 1 : 0)})");
    }

    private static PageRequest Page(int? limit = null, int? offset = null) => PageRequest.Create(limit, offset);

    [Fact]
    public async Task A_set_of_vitals_is_found_by_uuid_through_its_encounter()
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00");
        await using var db = fixture.CreateDbContext();

        var view = await new EfVitalsReader(db).FindAsync(Encounter, V1, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(V1, view.Uuid);
        Assert.Equal(150m, view.WeightPounds);
        Assert.Equal(120, view.Systolic);
        Assert.Equal(80, view.Diastolic);
        Assert.Equal(64, view.Pulse);
        Assert.Equal("dr_who", view.RecordedBy);
    }

    [Fact]
    public async Task CV_12_zeros_read_back_as_not_recorded_not_as_a_zero_measurement()
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00", weight: "0.000000", pulse: "0.000000");
        await using var db = fixture.CreateDbContext();

        var view = await new EfVitalsReader(db).FindAsync(Encounter, V1, CancellationToken.None);

        Assert.Null(view!.WeightPounds);
        Assert.Null(view.HeightInches);
        Assert.Null(view.Pulse);
        Assert.Null(view.Bmi);
    }

    [Theory]
    [InlineData("120/80")]
    [InlineData("high")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DOM_9_free_text_blood_pressure_legacy_may_hold_reads_as_absent_not_an_error(string text)
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00", bps: text);
        await using var db = fixture.CreateDbContext();

        var view = await new EfVitalsReader(db).FindAsync(Encounter, V1, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Null(view.Systolic);
        Assert.Equal(80, view.Diastolic);
    }

    [Fact]
    public async Task CV_14_vitals_of_another_encounter_or_patient_are_not_returned()
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00", encounter: 1001, pid: 42);
        await InsertVitalsAsync(V2, "2026-11-11 09:00:00", encounter: 2002, pid: 42);   // same patient, other encounter
        await InsertVitalsAsync(V3, "2026-11-12 09:00:00", encounter: 1001, pid: 99);   // same number, other patient
        await using var db = fixture.CreateDbContext();
        var reader = new EfVitalsReader(db);

        var page = await reader.ListForEncounterAsync(Encounter, Page(), CancellationToken.None);

        Assert.Equal([V1], page.Items.Select(i => i.Uuid));
        Assert.Null(await reader.FindAsync(Encounter, V2, CancellationToken.None));
        Assert.Null(await reader.FindAsync(Encounter, V3, CancellationToken.None));
    }

    [Fact]
    public async Task CV_14_deleted_vitals_are_absent_from_the_find_the_page_and_the_total()
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00");
        await InsertVitalsAsync(V2, "2026-11-11 09:00:00", deleted: true);
        await using var db = fixture.CreateDbContext();
        var reader = new EfVitalsReader(db);

        var page = await reader.ListForEncounterAsync(Encounter, Page(), CancellationToken.None);

        Assert.Equal([V1], page.Items.Select(i => i.Uuid));
        Assert.Equal(1, page.Total);
        Assert.Null(await reader.FindAsync(Encounter, V2, CancellationToken.None));
    }

    [Fact]
    public async Task A_duplicated_forms_row_does_not_duplicate_the_vitals()
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00");
        var id = await fixture.RootScalarAsync<long>("SELECT id FROM form_vitals");
        await fixture.RootExecuteAsync(
            $"INSERT INTO forms (date, encounter, form_name, form_id, pid, formdir, deleted) VALUES (NOW(), 1001, 'Vitals', {id}, 42, 'vitals', 0)");
        await using var db = fixture.CreateDbContext();

        var page = await new EfVitalsReader(db).ListForEncounterAsync(Encounter, Page(), CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task CV_14_the_list_is_newest_first_and_pages_are_disjoint()
    {
        await InsertVitalsAsync(V1, "2026-11-10 09:00:00");
        await InsertVitalsAsync(V2, "2026-11-11 09:00:00");
        await InsertVitalsAsync(V3, "2026-11-12 09:00:00");
        await using var db = fixture.CreateDbContext();
        var reader = new EfVitalsReader(db);

        var first = await reader.ListForEncounterAsync(Encounter, Page(2, 0), CancellationToken.None);
        var second = await reader.ListForEncounterAsync(Encounter, Page(2, 2), CancellationToken.None);

        Assert.Equal([V3, V2], first.Items.Select(i => i.Uuid));
        Assert.Equal([V1], second.Items.Select(i => i.Uuid));
        Assert.Equal(3, first.Total);
    }

    [Fact]
    public async Task What_the_registry_wrote_reads_back_through_the_reader()
    {
        var vitals = VitalsRegistryTests.NewVitals();
        await using (var db = fixture.CreateDbContext())
            await new EfVitalsRegistry(db, new EfAuditLog(db)).RegisterAsync(
                vitals,
                new(vitals.Uuid, vitals.Encounter.Uuid, 1001, EncounterTestData.PatientUuid, 42, vitals.MeasurementsTaken, DateTime.UtcNow),
                VitalsRegistryTests.AuditFor(vitals),
                CancellationToken.None);
        await using var db2 = fixture.CreateDbContext();

        var view = await new EfVitalsReader(db2).FindAsync(Encounter, vitals.Uuid, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(154.323584m, view.WeightPounds);
        Assert.Equal(68.897638m, view.HeightInches);
        Assert.Equal(98.6m, view.TemperatureFahrenheit);
        Assert.Equal("Oral", view.TemperatureMethod);
        Assert.Equal(22.854881m, view.Bmi);
        Assert.Equal("Normal", view.BmiStatus);
        Assert.Equal(98m, view.OxygenSaturation);
        Assert.Equal("calm", view.Note);
    }
}
