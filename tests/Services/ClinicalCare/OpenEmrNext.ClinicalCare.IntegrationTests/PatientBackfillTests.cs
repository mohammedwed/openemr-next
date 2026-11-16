using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The one-time provisioning backfill (ADR 0064 #5) and the retirement of the <c>patient_data</c> stand-in, both done by
/// the real seed script.
/// </summary>
[Collection("ClinicalCareContainer")]
public class PatientBackfillTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Legacy = Guid.Parse("c0c0c0c0-0000-4000-8000-000000000001");
    private static readonly Guid FromEvent = Guid.Parse("c0c0c0c0-0000-4000-8000-000000000002");

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_patient_registered_before_events_existed_is_copied_by_the_seed_script_with_source_backfill()
    {
        await fixture.SeedLegacyPatientAsync(Legacy, 501);

        await fixture.RunSeedScriptAsync();

        Assert.Equal("backfill", await fixture.RootScalarAsync<string>($"SELECT source FROM clinical_care_patient_ref WHERE pid = 501"));
    }

    [Fact]
    public async Task Rerunning_the_backfill_is_harmless_and_never_overwrites_a_reference_an_event_wrote()
    {
        await fixture.SeedPatientAsync(FromEvent, 502);               // written by an event (source 'event')
        await fixture.SeedLegacyPatientAsync(FromEvent, 502);          // and legacy holds the same patient
        await fixture.RunSeedScriptAsync();
        await fixture.RunSeedScriptAsync();

        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref WHERE pid = 502"));
        Assert.Equal("event", await fixture.RootScalarAsync<string>("SELECT source FROM clinical_care_patient_ref WHERE pid = 502"));
    }

    [Fact]
    public async Task A_legacy_row_without_a_uuid_or_with_a_zero_pid_is_not_copied()
    {
        await fixture.RootExecuteAsync("INSERT INTO patient_data (uuid, pid, fname) VALUES (NULL, 601, 'no uuid')");
        await fixture.RootExecuteAsync($"INSERT INTO patient_data (uuid, pid, fname) VALUES (UNHEX('{Guid.NewGuid():N}'), 0, 'zero pid')");

        await fixture.RunSeedScriptAsync();

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }

    [Fact]
    public async Task The_seed_script_revokes_what_it_no_longer_grants_so_an_old_database_loses_the_stand_in_grant()
    {
        // Simulate a database that ran the previous version of the script: the stand-in grant exists.
        await fixture.RootExecuteAsync($"GRANT SELECT (uuid, pid) ON {fixture.Database}.patient_data TO '{ClinicalCareContainerFixture.User}'@'%'");
        await using (var before = fixture.CreateDbContext())
        {
            // Proves the precondition: with the old grant the principal CAN read uuid and pid.
            var visible = await before.Database
                .SqlQueryRaw<long>( "SELECT COUNT(*) AS Value FROM (SELECT uuid, pid FROM patient_data) t").ToListAsync();
            Assert.Single(visible);
        }

        await fixture.RunSeedScriptAsync();

        await using var after = fixture.CreateDbContext();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            after.Database.ExecuteSqlRawAsync("SELECT uuid FROM patient_data"));
        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
