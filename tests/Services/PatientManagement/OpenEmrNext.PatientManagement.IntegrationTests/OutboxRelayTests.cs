using MySqlConnector;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// The relay's database half (EVT-7) against a real MariaDB: strict order, leases, back-off, dead letters and concurrent
/// relays. The tests clear the outbox first and use their own rows, so they do not depend on what other tests registered.
/// </summary>
[Collection("PatientDataContainer")]
public class OutboxRelayTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    public Task DisposeAsync() => Task.CompletedTask;

    public async Task InitializeAsync() => await ExecAsync("DELETE FROM patient_management_outbox");

    private async Task ExecAsync(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default! : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private static string Payload(long n) =>
        $$"""{"Uuid":"b1a9c5f0-0000-4000-8000-{{n:D12}}","Pid":{{n}},"FirstName":"A","LastName":"B","RegisteredAtUtc":"2026-11-16T09:00:00Z"}""";

    /// <summary>Inserts rows with ids 1..count.</summary>
    private async Task SeedAsync(int count)
    {
        for (var i = 1; i <= count; i++)
            await ExecAsync($"INSERT INTO patient_management_outbox (id, event_type, payload, occurred_at) VALUES ({i}, 'PatientRegistered', '{Payload(i)}', UTC_TIMESTAMP())");
    }

    private async Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int batch = 10, int leaseSeconds = 60)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfOutboxRelayStore(db).ClaimAsync(batch, TimeSpan.FromSeconds(leaseSeconds), CancellationToken.None);
    }

    [Fact]
    public async Task Rows_are_claimed_oldest_first_and_only_unpublished_ones()
    {
        await SeedAsync(4);
        await ExecAsync("UPDATE patient_management_outbox SET published_at = UTC_TIMESTAMP() WHERE id = 1");

        var claimed = await ClaimAsync();

        Assert.Equal([2L, 3L, 4L], claimed.Select(c => c.Id));
    }

    [Fact]
    public async Task The_batch_size_is_respected()
    {
        await SeedAsync(5);

        Assert.Equal([1L, 2L], (await ClaimAsync(batch: 2)).Select(c => c.Id));
    }

    [Fact]
    public async Task A_claimed_row_is_leased_so_it_is_not_claimed_again_while_the_lease_is_held()
    {
        await SeedAsync(2);

        var first = await ClaimAsync();
        var second = await ClaimAsync();

        Assert.Equal(2, first.Count);
        Assert.Empty(second);
    }

    [Fact]
    public async Task A_relay_that_dies_loses_nothing_the_lease_expires_and_the_rows_are_claimed_again()
    {
        await SeedAsync(2);
        await ClaimAsync();                                                                  // claimed, then the relay "crashes"
        await ExecAsync("UPDATE patient_management_outbox SET locked_until = UTC_TIMESTAMP() - INTERVAL 1 SECOND");

        Assert.Equal([1L, 2L], (await ClaimAsync()).Select(c => c.Id));
    }

    [Fact]
    public async Task A_published_row_is_never_claimed_again_and_loses_its_lease()
    {
        await SeedAsync(1);
        await ClaimAsync();
        await using (var db = fixture.CreateDbContext())
            await new EfOutboxRelayStore(db).MarkPublishedAsync(1, CancellationToken.None);

        Assert.Empty(await ClaimAsync());
        Assert.NotNull(await ScalarAsync<DateTime?>("SELECT published_at FROM patient_management_outbox WHERE id = 1"));
        Assert.Null(await ScalarAsync<DateTime?>("SELECT locked_until FROM patient_management_outbox WHERE id = 1"));
    }

    [Fact]
    public async Task A_failed_row_backs_off_and_BLOCKS_the_rows_behind_it_so_order_is_kept()
    {
        await SeedAsync(3);
        await ClaimAsync();
        await using (var db = fixture.CreateDbContext())
            await new EfOutboxRelayStore(db).MarkFailedAsync(1, "down", TimeSpan.FromMinutes(5), deadLetter: false, CancellationToken.None);
        await ExecAsync("UPDATE patient_management_outbox SET locked_until = NULL WHERE id IN (2, 3)");

        var claimed = await ClaimAsync();

        Assert.Empty(claimed);   // row 1 is not due yet, and 2 and 3 must not overtake it
        Assert.Equal(1, await ScalarAsync<int>("SELECT attempts FROM patient_management_outbox WHERE id = 1"));
        Assert.Equal("down", await ScalarAsync<string>("SELECT last_error FROM patient_management_outbox WHERE id = 1"));
    }

    [Fact]
    public async Task A_failed_row_is_claimed_again_once_its_back_off_has_passed()
    {
        await SeedAsync(2);
        await ClaimAsync();
        await using (var db = fixture.CreateDbContext())
            await new EfOutboxRelayStore(db).MarkFailedAsync(1, "down", TimeSpan.FromMinutes(5), deadLetter: false, CancellationToken.None);
        await ExecAsync("UPDATE patient_management_outbox SET next_attempt_at = UTC_TIMESTAMP() - INTERVAL 1 SECOND, locked_until = NULL");

        Assert.Equal([1L, 2L], (await ClaimAsync()).Select(c => c.Id));
    }

    [Fact]
    public async Task A_dead_lettered_row_is_skipped_and_the_rows_behind_it_flow_again()
    {
        await SeedAsync(3);
        await ClaimAsync();
        await using (var db = fixture.CreateDbContext())
            await new EfOutboxRelayStore(db).MarkFailedAsync(1, "poison", TimeSpan.Zero, deadLetter: true, CancellationToken.None);
        await ExecAsync("UPDATE patient_management_outbox SET locked_until = NULL");

        Assert.Equal([2L, 3L], (await ClaimAsync()).Select(c => c.Id));
        Assert.NotNull(await ScalarAsync<DateTime?>("SELECT dead_lettered_at FROM patient_management_outbox WHERE id = 1"));
    }

    [Fact]
    public async Task Releasing_gives_leased_rows_back_immediately()
    {
        await SeedAsync(3);
        await ClaimAsync();
        await using (var db = fixture.CreateDbContext())
            await new EfOutboxRelayStore(db).ReleaseAsync([2L, 3L], CancellationToken.None);
        await ExecAsync("UPDATE patient_management_outbox SET locked_until = UTC_TIMESTAMP() + INTERVAL 1 HOUR WHERE id = 1"); // row 1 stays held

        // Row 1 is still leased, so nothing behind it may be claimed, even though 2 and 3 were released.
        Assert.Empty(await ClaimAsync());
        await ExecAsync("UPDATE patient_management_outbox SET locked_until = NULL WHERE id = 1");
        Assert.Equal([1L, 2L, 3L], (await ClaimAsync()).Select(c => c.Id));
    }

    [Fact]
    public async Task OPS_19_concurrent_relays_never_claim_the_same_row_twice()
    {
        await SeedAsync(30);

        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ClaimAsync(batch: 10)));

        var all = claims.SelectMany(c => c.Select(e => e.Id)).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());     // no row handed to two relays
        Assert.Equal(10, all.Count);                          // one relay got the head of the queue; the others saw it leased and stopped
        Assert.Equal(Enumerable.Range(1, 10).Select(i => (long)i), all.Order());
    }

    [Fact]
    public async Task The_relay_use_case_drains_real_registrations_through_the_database_exactly_once_each()
    {
        // Real registrations write real outbox rows (PascalCase, the shape that existed before any consumer did).
        var uuids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            await using var context = fixture.CreateDbContext();
            var patient = await new RegisterPatientUseCase(new EfPatientRegistry(context)).ExecuteAsync(
                PatientRegistration.Create("Ada", $"Lovelace{i}", "Female", new DateOnly(1990, 1, 1)), 1, DateTime.UtcNow, CancellationToken.None);
            uuids.Add(patient.Uuid);
        }

        var published = new List<OutboundEvent>();
        var publisher = new CapturingPublisher(published);
        await using var db = fixture.CreateDbContext();
        var relay = new RelayOutboxUseCase(new EfOutboxRelayStore(db), publisher, RelayPolicy.Default);

        var first = await relay.ExecuteAsync(CancellationToken.None);
        var second = await relay.ExecuteAsync(CancellationToken.None);

        Assert.Equal(3, first.Published);
        Assert.Equal(0, second.Published);                                         // nothing is delivered twice
        Assert.Equal(uuids, published.Select(e => Guid.Parse(e.Subject)));         // in registration order
        Assert.Equal(published.Count, published.Select(e => e.EventId).Distinct().Count());
        Assert.Equal(3L, await ScalarAsync<long>("SELECT COUNT(*) FROM patient_management_outbox WHERE published_at IS NOT NULL"));
    }

    [Fact]
    public async Task A_consumer_that_is_down_leaves_the_event_in_the_outbox_with_its_error_and_a_retry_time()
    {
        await using (var context = fixture.CreateDbContext())
            await new RegisterPatientUseCase(new EfPatientRegistry(context)).ExecuteAsync(
                PatientRegistration.Create("Ada", "Lovelace", "Female", new DateOnly(1990, 1, 1)), 1, DateTime.UtcNow, CancellationToken.None);

        await using var db = fixture.CreateDbContext();
        var result = await new RelayOutboxUseCase(new EfOutboxRelayStore(db), new FailingPublisher(), RelayPolicy.Default)
            .ExecuteAsync(CancellationToken.None);

        Assert.Equal(new RelayResult(0, 1, 0), result);
        Assert.Null(await ScalarAsync<DateTime?>("SELECT published_at FROM patient_management_outbox"));
        Assert.Equal(1, await ScalarAsync<int>("SELECT attempts FROM patient_management_outbox"));
        Assert.Contains("consumer is down", await ScalarAsync<string>("SELECT last_error FROM patient_management_outbox"));
        Assert.True(await ScalarAsync<int>("SELECT next_attempt_at > UTC_TIMESTAMP() FROM patient_management_outbox") == 1);
    }

    private sealed class CapturingPublisher(List<OutboundEvent> sink) : IEventPublisher
    {
        public Task PublishAsync(OutboundEvent @event, CancellationToken ct) { sink.Add(@event); return Task.CompletedTask; }
    }

    private sealed class FailingPublisher : IEventPublisher
    {
        public Task PublishAsync(OutboundEvent @event, CancellationToken ct) =>
            throw new EventPublishException("the consumer is down", isPermanent: false);
    }
}
