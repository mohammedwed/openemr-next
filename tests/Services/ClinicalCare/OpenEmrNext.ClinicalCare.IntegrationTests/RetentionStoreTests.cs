using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Retention (finding R-10) against a real MariaDB, as the production principal: old bookkeeping goes, recent bookkeeping stays, and
/// nothing outside this service's own tables is ever touched.
/// </summary>
[Collection("ClinicalCareContainer")]
public class RetentionStoreTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly RetentionCutoffs Cutoffs = new(ProcessedEventDays: 30, PublishedOutboxDays: 30, UnpublishedOutboxDays: 365);

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private Task ProcessedAsync(string daysAgo, string tag) =>
        fixture.RootExecuteAsync($"INSERT INTO clinical_care_processed_events (event_id, event_type, processed_at) VALUES (UUID(), '{tag}', DATE_SUB(NOW(), INTERVAL {daysAgo} DAY))");

    private Task OutboxAsync(string tag, string occurredDaysAgo, string? publishedDaysAgo) =>
        fixture.RootExecuteAsync(
            "INSERT INTO clinical_care_outbox (event_type, payload, occurred_at, published_at) VALUES " +
            $"('{tag}', '{{}}', DATE_SUB(UTC_TIMESTAMP(), INTERVAL {occurredDaysAgo} DAY), " +
            (publishedDaysAgo is null ? "NULL)" : $"DATE_SUB(UTC_TIMESTAMP(), INTERVAL {publishedDaysAgo} DAY))"));

    private async Task<PurgeResult> RunAsync(int batchSize = 1000)
    {
        await using var db = fixture.CreateDbContext();
        return await new PurgeExpiredRecordsUseCase(new EfRetentionStore(db), Cutoffs, batchSize).ExecuteAsync(CancellationToken.None);
    }

    private Task<long> CountAsync(string table, string tag) =>
        fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM {table} WHERE event_type = '{tag}'");

    [Fact]
    public async Task A_processed_event_id_older_than_the_window_is_removed_and_a_recent_one_is_kept()
    {
        await ProcessedAsync("31", "old");
        await ProcessedAsync("29", "recent");
        await ProcessedAsync("0", "today");

        var result = await RunAsync();

        Assert.Equal(1, result.ProcessedEvents);
        Assert.Equal(0L, await CountAsync("clinical_care_processed_events", "old"));
        Assert.Equal(1L, await CountAsync("clinical_care_processed_events", "recent"));
        Assert.Equal(1L, await CountAsync("clinical_care_processed_events", "today"));
    }

    [Fact]
    public async Task A_delivered_outbox_row_ages_from_when_it_was_delivered_not_when_it_happened()
    {
        await OutboxAsync("delivered_long_ago", occurredDaysAgo: "90", publishedDaysAgo: "40");
        await OutboxAsync("happened_long_ago_delivered_yesterday", occurredDaysAgo: "90", publishedDaysAgo: "1");

        var result = await RunAsync();

        Assert.Equal(1, result.PublishedOutbox);
        Assert.Equal(0L, await CountAsync("clinical_care_outbox", "delivered_long_ago"));
        Assert.Equal(1L, await CountAsync("clinical_care_outbox", "happened_long_ago_delivered_yesterday"));
    }

    [Fact]
    public async Task An_undelivered_outbox_row_is_kept_for_the_long_window_because_nothing_consumes_it_yet()
    {
        await OutboxAsync("undelivered_100_days", occurredDaysAgo: "100", publishedDaysAgo: null);
        await OutboxAsync("undelivered_400_days", occurredDaysAgo: "400", publishedDaysAgo: null);

        var result = await RunAsync();

        Assert.Equal(1, result.UnpublishedOutbox);
        Assert.Equal(1L, await CountAsync("clinical_care_outbox", "undelivered_100_days"));   // would be gone on the 30-day delivered window
        Assert.Equal(0L, await CountAsync("clinical_care_outbox", "undelivered_400_days"));
    }

    [Fact]
    public async Task A_backlog_larger_than_one_batch_is_fully_cleared_in_several_short_deletes()
    {
        for (var i = 0; i < 25; i++)
            await ProcessedAsync("60", "backlog");

        var result = await RunAsync(batchSize: 10);

        Assert.Equal(25, result.ProcessedEvents);
        Assert.Equal(0L, await CountAsync("clinical_care_processed_events", "backlog"));
    }

    [Fact]
    public async Task Nothing_due_removes_nothing_and_leaves_clinical_data_and_references_alone()
    {
        await fixture.SeedPatientAsync(EncounterTestData.PatientUuid, EncounterTestData.PatientPid);
        await fixture.RootExecuteAsync("INSERT INTO form_encounter (uuid, date, pid, encounter, reason) VALUES (UNHEX('11111111111141118111111111111111'), '1999-01-01', 42, 500, 'old visit')");
        await fixture.RootExecuteAsync("INSERT INTO form_vitals (uuid, date, pid, weight) VALUES (UNHEX('22222222222242228222222222222222'), '1999-01-01', 42, 150)");

        var result = await RunAsync();

        Assert.Equal(0, result.Total);
        // Clinical data is never purged by retention, however old: a 1999 encounter and vitals are untouched.
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_encounter"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM form_vitals"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }

    [Fact]
    public async Task The_production_principal_can_do_exactly_this_and_still_cannot_delete_clinical_data()
    {
        // Retention runs as the production principal (it can delete its own tables) and that grant does not leak to legacy tables.
        await using var db = fixture.CreateDbContext();
        var denied = await Assert.ThrowsAnyAsync<Exception>(() =>
            Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database, "DELETE FROM form_encounter WHERE date < '1900-01-01'"));

        Assert.Contains("denied", denied.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
