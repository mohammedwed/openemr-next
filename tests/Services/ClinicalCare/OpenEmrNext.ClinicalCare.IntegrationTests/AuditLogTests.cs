using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The audit trail (SEC-9, ADR 0069) against a real MariaDB, as the production principal: a row round-trips, it stands or falls with the
/// transaction it is written in, and the principal can add rows but never edit or remove one.
/// </summary>
[Collection("ClinicalCareContainer")]
public class AuditLogTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTime At = new(2026, 11, 17, 9, 30, 15, 123, DateTimeKind.Utc);
    private static readonly Guid Subject = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static AuditEntry Entry(AuditOutcome outcome = AuditOutcome.Success) =>
        new(At, new AuditActor("user:7", 7, Verified: false), AuditActions.EncounterUpdate, Subject, outcome,
            correlationId: "corr-1", sourceAddress: "10.0.0.5", changedFields: ["reason", "date"]);

    private Task<long> CountAsync() => fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_audit");

    [Fact]
    public async Task An_entry_round_trips_every_column()
    {
        await using var db = fixture.CreateDbContext();

        await new EfAuditLog(db).AppendAsync(Entry(), CancellationToken.None);

        Assert.Equal(1L, await CountAsync());
        Assert.Equal("user:7", await fixture.RootScalarAsync<string>("SELECT actor FROM clinical_care_audit"));
        Assert.Equal(7L, await fixture.RootScalarAsync<long>("SELECT actor_id FROM clinical_care_audit"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT actor_verified FROM clinical_care_audit"));
        Assert.Equal("encounter.update", await fixture.RootScalarAsync<string>("SELECT action FROM clinical_care_audit"));
        Assert.Equal(Subject.ToString(), await fixture.RootScalarAsync<string>("SELECT CONCAT(subject, '') FROM clinical_care_audit"));
        Assert.Equal("success", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
        Assert.Equal("corr-1", await fixture.RootScalarAsync<string>("SELECT correlation_id FROM clinical_care_audit"));
        Assert.Equal("10.0.0.5", await fixture.RootScalarAsync<string>("SELECT source_address FROM clinical_care_audit"));
        Assert.Equal("""["reason","date"]""", await fixture.RootScalarAsync<string>("SELECT details FROM clinical_care_audit"));
    }

    [Fact]
    public async Task The_instant_is_stored_with_sub_second_precision_and_not_shifted()
    {
        await using var db = fixture.CreateDbContext();

        await new EfAuditLog(db).AppendAsync(Entry(), CancellationToken.None);

        Assert.Equal("2026-11-17 09:30:15.123000",
            await fixture.RootScalarAsync<string>("SELECT DATE_FORMAT(at, '%Y-%m-%d %H:%i:%s.%f') FROM clinical_care_audit"));
    }

    [Fact]
    public async Task An_entry_without_optional_parts_stores_nulls()
    {
        await using var db = fixture.CreateDbContext();
        var bare = new AuditEntry(At, new AuditActor("user:7", null, false), AuditActions.VitalsRecord, Subject, AuditOutcome.Failed);

        await new EfAuditLog(db).AppendAsync(bare, CancellationToken.None);

        Assert.Equal(1L, await fixture.RootScalarAsync<long>(
            "SELECT COUNT(*) FROM clinical_care_audit WHERE actor_id IS NULL AND correlation_id IS NULL AND source_address IS NULL AND details IS NULL"));
        Assert.Equal("failed", await fixture.RootScalarAsync<string>("SELECT outcome FROM clinical_care_audit"));
    }

    [Fact]
    public async Task A_row_written_in_a_transaction_that_rolls_back_is_gone()
    {
        await using var db = fixture.CreateDbContext();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await new EfAuditLog(db).AppendAsync(Entry(), CancellationToken.None);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0L, await CountAsync());
    }

    [Fact]
    public async Task A_row_written_in_a_transaction_that_commits_is_kept()
    {
        await using var db = fixture.CreateDbContext();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await new EfAuditLog(db).AppendAsync(Entry(), CancellationToken.None);
            await transaction.CommitAsync();
        }

        Assert.Equal(1L, await CountAsync());
    }

    [Fact]
    public async Task The_principal_can_read_the_trail_back()
    {
        await using var db = fixture.CreateDbContext();
        await new EfAuditLog(db).AppendAsync(Entry(), CancellationToken.None);

        var count = await db.Database.SqlQueryRaw<long>("SELECT COUNT(*) AS Value FROM clinical_care_audit").SingleAsync();

        Assert.Equal(1L, count);
    }

    [Theory]
    [InlineData("UPDATE clinical_care_audit SET outcome = 'success'")]
    [InlineData("UPDATE clinical_care_audit SET details = NULL")]
    [InlineData("DELETE FROM clinical_care_audit")]
    [InlineData("TRUNCATE TABLE clinical_care_audit")]
    [InlineData("DROP TABLE clinical_care_audit")]
    public async Task The_principal_can_never_edit_or_remove_an_audit_row(string sql)
    {
        await using var db = fixture.CreateDbContext();
        await new EfAuditLog(db).AppendAsync(Entry(), CancellationToken.None);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("denied", ex.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1L, await CountAsync());
    }

    [Fact]
    public async Task The_widths_AuditEntry_cuts_to_match_the_columns_the_seed_script_creates()
    {
        var script = await File.ReadAllTextAsync(RepoFiles.Find("scripts/seeds/clinical-care-db.sh"));
        int Width(string column) =>
            int.Parse(Regex.Match(script, $@"^\s*{column} VARCHAR\((\d+)\)", RegexOptions.Multiline).Groups[1].Value);

        Assert.Equal(AuditEntry.MaxActorLength, Width("actor"));
        Assert.Equal(AuditEntry.MaxActionLength, Width("action"));
        Assert.Equal(AuditEntry.MaxCorrelationLength, Width("correlation_id"));
        Assert.Equal(AuditEntry.MaxAddressLength, Width("source_address"));
        Assert.Equal(AuditEntry.MaxDetailsLength, Width("details"));
    }
}
