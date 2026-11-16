using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The read-side adapters: patient lookup (from Clinical Care's own reference table, ADR 0066), encounter classes and facilities. Each reads
/// legacy tolerantly (DOM-9, LEG-4) and, run as the production principal, also proves the column-level grants
/// are sufficient for exactly these queries.
/// </summary>
[Collection("ClinicalCareContainer")]
public class LegacyReferenceAdapterTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CC_1_a_registered_patient_is_found_by_uuid_with_the_pid()
    {
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, 42);
        await using var db = fixture.CreateDbContext();

        var patient = await new EfPatientDirectory(db).FindAsync(EncounterTestData.PatientUuid, CancellationToken.None);

        Assert.NotNull(patient);
        Assert.Equal(EncounterTestData.PatientUuid, patient.Uuid);
        Assert.Equal(42, patient.Pid);
    }

    [Fact]
    public async Task CC_1_an_unknown_uuid_is_not_found()
    {
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfPatientDirectory(db).FindAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CC_1_a_reference_with_pid_zero_is_treated_as_not_found_not_as_a_crash()
    {
        // A bad event must not hand out a reference legacy tables cannot join on (DOM-9): pid 0 is treated as unknown.
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, 0);
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfPatientDirectory(db).FindAsync(EncounterTestData.PatientUuid, CancellationToken.None));
    }

    [Fact]
    public async Task CC_3_classes_come_back_in_seq_order_with_the_default_flagged_and_other_lists_excluded()
    {
        await using var db = fixture.CreateDbContext();

        var classes = await new EfEncounterCodeLists(db).GetEncounterClassesAsync(CancellationToken.None);

        Assert.Equal(["AMB", "EMER", "ACUTE"], classes.Select(c => c.Code));
        Assert.Equal(["AMB"], classes.Where(c => c.IsDefault).Select(c => c.Code));
        Assert.Equal("Outpatient", classes[0].Title);
    }

    [Fact]
    public async Task CC_8_a_facility_is_found_with_its_name_and_pos_code()
    {
        await using var db = fixture.CreateDbContext();

        var facility = await new EfFacilityDirectory(db).FindAsync(3, CancellationToken.None);

        Assert.NotNull(facility);
        Assert.Equal("Main Clinic", facility.Name);
        Assert.Equal(11, facility.PosCode);
    }

    [Fact]
    public async Task CC_8_a_facility_without_a_pos_code_has_a_null_one()
    {
        await using var db = fixture.CreateDbContext();

        var facility = await new EfFacilityDirectory(db).FindAsync(4, CancellationToken.None);

        Assert.NotNull(facility);
        Assert.Null(facility.PosCode);
    }

    [Fact]
    public async Task CC_8_an_unknown_facility_is_not_found()
    {
        await using var db = fixture.CreateDbContext();

        Assert.Null(await new EfFacilityDirectory(db).FindAsync(99, CancellationToken.None));
    }
}
