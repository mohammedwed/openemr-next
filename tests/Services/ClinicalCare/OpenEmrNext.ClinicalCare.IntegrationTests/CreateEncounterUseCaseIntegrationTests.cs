using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>The whole slice below the HTTP layer: the real use case wired to every real adapter, as the production principal.</summary>
[Collection("ClinicalCareContainer")]
public class CreateEncounterUseCaseIntegrationTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateEncounterCommand Command(int category = 5, int facilityId = 3) =>
        new(EncounterTestData.PatientUuid, null, category, null, "Cough", facilityId, null, 7, null, null);

    private static CreateEncounterUseCase Build(ClinicalCareDbContext db) =>
        new(new EfPatientDirectory(db), new EfEncounterCodeLists(db), new EfFacilityDirectory(db),
            new EfEncounterIdentityAllocator(db), new EfEncounterRegistry(db), TimeProvider.System);

    [Fact]
    public async Task Creating_an_encounter_for_a_registered_patient_lands_in_every_legacy_table_and_the_outbox()
    {
        await using var db = fixture.CreateDbContext();

        var encounter = await Build(db).ExecuteAsync(Command(), CancellationToken.None);

        Assert.Equal(1001, encounter.Number);
        Assert.Equal("AMB", encounter.ClassCode);
        Assert.Equal("Main Clinic", encounter.FacilityName);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM form_encounter WHERE id = {encounter.Id}"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM forms WHERE formdir = 'newpatient'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM uuid_registry"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_outbox"));
    }

    [Fact]
    public async Task CC_1_an_unknown_patient_is_not_found_and_writes_nothing_and_takes_no_number()
    {
        await using var db = fixture.CreateDbContext();
        var command = Command() with { PatientUuid = Guid.NewGuid() };

        await Assert.ThrowsAsync<PatientNotFoundException>(() => Build(db).ExecuteAsync(command, CancellationToken.None));

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
        Assert.Equal(1000L, await fixture.RootScalarAsync<long>("SELECT id FROM sequences"));
    }

    [Fact]
    public async Task CC_5_a_rejected_request_takes_no_number_from_the_shared_counter()
    {
        await using var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<EncounterValidationException>(() => Build(db).ExecuteAsync(Command(category: 0), CancellationToken.None));

        Assert.Equal(1000L, await fixture.RootScalarAsync<long>("SELECT id FROM sequences"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
    }

    [Fact]
    public async Task CC_8_an_unknown_facility_is_rejected_before_anything_is_written()
    {
        await using var db = fixture.CreateDbContext();

        var ex = await Assert.ThrowsAsync<EncounterValidationException>(() => Build(db).ExecuteAsync(Command(facilityId: 99), CancellationToken.None));

        Assert.Contains("Facility 99 does not exist.", ex.Errors);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
    }

    [Fact]
    public async Task Two_encounters_for_the_same_patient_get_distinct_numbers_and_uuids()
    {
        await using var db = fixture.CreateDbContext();
        var useCase = Build(db);

        var first = await useCase.ExecuteAsync(Command(), CancellationToken.None);
        var second = await useCase.ExecuteAsync(Command(), CancellationToken.None);

        Assert.NotEqual(first.Number, second.Number);
        Assert.NotEqual(first.Uuid, second.Uuid);
        Assert.Equal(2L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
    }
}
