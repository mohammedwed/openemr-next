using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// LEG-6 as a test: the principal the seed script creates can do what the service needs, and nothing more.
/// Each denial is a statement a future change might be tempted to add; if the grant set widens, this fails.
/// </summary>
[Collection("ClinicalCareContainer")]
public class LeastPrivilegeGrantTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task AssertDeniedAsync(string sql)
    {
        await using var db = fixture.CreateDbContext();

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UPDATE form_encounter SET reason = 'changed'")]
    [InlineData("DELETE FROM form_encounter")]
    [InlineData("UPDATE forms SET deleted = 1")]
    [InlineData("DELETE FROM forms")]
    [InlineData("DELETE FROM uuid_registry")]
    [InlineData("DELETE FROM sequences")]
    [InlineData("UPDATE form_vitals SET weight = 1")]
    [InlineData("DELETE FROM form_vitals")]
    [InlineData("INSERT INTO patient_data (pid) VALUES (1)")]
    [InlineData("UPDATE patient_data SET fname = 'x'")]
    [InlineData("INSERT INTO users (username) VALUES ('intruder')")]
    [InlineData("DROP TABLE form_encounter")]
    public async Task The_production_principal_cannot_change_what_it_does_not_own(string sql) =>
        await AssertDeniedAsync(sql);

    [Theory]
    [InlineData("SELECT password FROM users")]
    [InlineData("SELECT * FROM users")]
    [InlineData("SELECT fname FROM patient_data")]
    [InlineData("SELECT * FROM patient_data")]
    [InlineData("SELECT uuid FROM patient_data")]
    [InlineData("SELECT pid FROM patient_data")]
    [InlineData("SELECT phone FROM facility")]
    [InlineData("SELECT notes FROM list_options")]
    public async Task The_production_principal_cannot_read_columns_or_tables_it_has_no_need_for(string sql)
    {
        await using var db = fixture.CreateDbContext();

        // Reading is done through a command that actually executes (not a lazy query), so the privilege check runs.
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();
            command.CommandText = sql;
            await command.ExecuteScalarAsync();
        });

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_production_principal_owns_its_outbox_completely()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO clinical_care_outbox (event_type, payload, occurred_at) VALUES ('x', 'payload', NOW())");
        await db.Database.ExecuteSqlRawAsync("UPDATE clinical_care_outbox SET published_at = NOW()");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM clinical_care_outbox");
    }

    [Fact]
    public async Task Running_the_seed_script_again_is_harmless_and_the_principal_still_works()
    {
        // The fixture already ran it once. A second run must succeed and leave a working user (LEG-18).
        await fixture.RunSeedScriptAsync();
        await using var db = fixture.CreateDbContext();

        Assert.Equal(1000L, await db.Database.SqlQueryRaw<long>("SELECT id AS Value FROM sequences").SingleAsync());
    }
}
