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
    [InlineData("UPDATE form_encounter SET billing_note = 'changed'")]
    [InlineData("UPDATE form_encounter SET sensitivity = 'high'")]
    [InlineData("UPDATE form_encounter SET pid = 1")]
    [InlineData("UPDATE form_encounter SET encounter = 1")]
    [InlineData("UPDATE form_encounter SET uuid = NULL")]
    [InlineData("UPDATE form_encounter SET in_collection = 1")]
    [InlineData("UPDATE form_encounter SET last_level_billed = 1")]
    [InlineData("UPDATE form_encounter SET last_update = NOW()")]
    [InlineData("UPDATE form_encounter SET reason = 'changed', billing_note = 'x'")]
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

    [Theory]
    [InlineData("date")]
    [InlineData("onset_date")]
    [InlineData("reason")]
    [InlineData("class_code")]
    [InlineData("pc_catid")]
    [InlineData("facility_id")]
    [InlineData("facility")]
    [InlineData("pos_code")]
    [InlineData("provider_id")]
    public async Task The_production_principal_may_update_exactly_the_encounter_columns_it_owns(string column)
    {
        await using var db = fixture.CreateDbContext();

        // Setting a column to itself is enough to exercise the privilege check; no row is needed. The column name is one of this
        // theory's own constants, never input, which is why the interpolation (EF1002) is safe here.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"UPDATE form_encounter SET `{column}` = `{column}`");
#pragma warning restore EF1002
    }

    [Theory]
    [InlineData("SELECT id, uuid, `date`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, activity, pid, `user`, modifydate FROM lists")]
    [InlineData("SELECT pid, type, `date` FROM lists_touch")]
    public async Task The_production_principal_can_read_the_issue_columns_a_problem_uses(string sql)
    {
        await using var db = fixture.CreateDbContext();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        await db.Database.OpenConnectionAsync();
        command.CommandText = sql;

        await command.ExecuteScalarAsync();   // must not throw
    }

    [Theory]
    [InlineData("SELECT udi FROM lists")]
    [InlineData("SELECT udi_data FROM lists")]
    [InlineData("SELECT injury_part FROM lists")]
    [InlineData("SELECT injury_type FROM lists")]
    [InlineData("SELECT reinjury_id FROM lists")]
    [InlineData("SELECT destination FROM lists")]
    [InlineData("SELECT external_id FROM lists")]
    [InlineData("SELECT erx_source FROM lists")]
    [InlineData("SELECT * FROM lists")]
    [InlineData("SELECT * FROM lists_medication")]
    [InlineData("SELECT * FROM issue_encounter")]
    public async Task The_production_principal_cannot_read_issue_columns_or_tables_a_problem_has_no_need_for(string sql)
    {
        await using var db = fixture.CreateDbContext();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            await db.Database.OpenConnectionAsync();
            command.CommandText = sql;
            await command.ExecuteScalarAsync();
        });

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("UPDATE lists SET title = 'changed'")]
    [InlineData("UPDATE lists SET enddate = NOW()")]
    [InlineData("DELETE FROM lists")]
    [InlineData("DELETE FROM lists_touch WHERE pid = 1")]
    [InlineData("UPDATE lists_touch SET `date` = NOW()")]
    [InlineData("INSERT INTO lists (uuid, type, udi) VALUES (NULL, 'medical_problem', 'x')")]
    [InlineData("INSERT INTO lists (uuid, type, reaction) VALUES (NULL, 'allergy', 'x')")]
    [InlineData("INSERT INTO lists (uuid, type, injury_part) VALUES (NULL, 'medical_problem', 'x')")]
    [InlineData("INSERT INTO lists_medication (list_id) VALUES (1)")]
    [InlineData("INSERT INTO issue_encounter (pid, list_id, encounter) VALUES (1, 1, 1)")]
    public async Task The_production_principal_cannot_change_or_remove_issues_beyond_inserting_a_problems_columns(string sql) =>
        await AssertDeniedAsync(sql);

    [Fact]
    public async Task The_production_principal_can_insert_exactly_the_columns_a_problem_uses()
    {
        await using var db = fixture.CreateDbContext();

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO lists (uuid, `date`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, activity, pid, `user`, groupname) " +
            "VALUES (NULL, NOW(), 'medical_problem', 't', NOW(), NULL, NULL, 0, 0, NULL, NULL, 1, 1, NULL, 'Default')");
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
