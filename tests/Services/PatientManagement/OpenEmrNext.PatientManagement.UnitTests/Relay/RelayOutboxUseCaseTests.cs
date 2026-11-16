using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.UnitTests.Relay;

/// <summary>EVT-7 as behaviour: order is kept, failures back off, poison events are dead-lettered, and nothing is lost.</summary>
public class RelayOutboxUseCaseTests
{
    private static readonly RelayPolicy Policy =
        new(BatchSize: 10, MaxAttempts: 3, Lease: TimeSpan.FromMinutes(1), BaseBackoff: TimeSpan.FromSeconds(5), MaxBackoff: TimeSpan.FromSeconds(40));

    private static string Payload(long n) =>
        $$"""{"Uuid":"b1a9c5f0-0000-4000-8000-{{n:D12}}","Pid":{{n}},"FirstName":"A","LastName":"B","RegisteredAtUtc":"2026-11-16T09:00:00Z"}""";

    private static OutboxEntry Row(long id, int attempts = 0, string? payload = null) =>
        new(id, "PatientRegistered", payload ?? Payload(id), new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc), attempts);

    private sealed class FakeStore(params OutboxEntry[] rows) : IOutboxRelayStore
    {
        public List<long> Published { get; } = [];
        public List<(long Id, string Error, TimeSpan RetryAfter, bool Dead)> Failed { get; } = [];
        public List<long> Released { get; } = [];

        public Task<IReadOnlyList<OutboxEntry>> ClaimAsync(int batchSize, TimeSpan lease, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OutboxEntry>>(rows.Take(batchSize).ToList());

        public Task MarkPublishedAsync(long id, CancellationToken ct) { Published.Add(id); return Task.CompletedTask; }

        public Task MarkFailedAsync(long id, string error, TimeSpan retryAfter, bool deadLetter, CancellationToken ct)
        {
            Failed.Add((id, error, retryAfter, deadLetter));
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(IReadOnlyCollection<long> ids, CancellationToken ct) { Released.AddRange(ids); return Task.CompletedTask; }
    }

    private sealed class FakePublisher(Func<OutboundEvent, Exception?> outcome) : IEventPublisher
    {
        public List<long> Attempted { get; } = [];

        public Task PublishAsync(OutboundEvent @event, CancellationToken ct)
        {
            Attempted.Add(long.Parse(@event.Subject.Split('-')[^1]));
            var failure = outcome(@event);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    [Fact]
    public async Task Rows_are_delivered_in_order_and_each_is_marked_published()
    {
        var store = new FakeStore(Row(1), Row(2), Row(3));
        var publisher = new FakePublisher(_ => null);

        var result = await new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None);

        Assert.Equal([1L, 2L, 3L], publisher.Attempted);
        Assert.Equal([1L, 2L, 3L], store.Published);
        Assert.Equal(new RelayResult(3, 0, 0), result);
    }

    [Fact]
    public async Task An_empty_outbox_does_nothing()
    {
        var result = await new RelayOutboxUseCase(new FakeStore(), new FakePublisher(_ => null), Policy).ExecuteAsync(CancellationToken.None);

        Assert.Equal(RelayResult.Nothing, result);
    }

    [Fact]
    public async Task A_transient_failure_backs_off_stops_the_pass_and_releases_the_rest_so_nothing_overtakes_it()
    {
        var store = new FakeStore(Row(1), Row(2), Row(3));
        var publisher = new FakePublisher(e => e.Subject.EndsWith("2".PadLeft(12, '0')) ? new EventPublishException("down", isPermanent: false) : null);

        var result = await new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None);

        Assert.Equal([1L, 2L], publisher.Attempted);        // row 3 was never tried
        Assert.Equal([1L], store.Published);
        Assert.Equal([2L], store.Failed.Select(f => f.Id));
        Assert.False(store.Failed[0].Dead);
        Assert.Equal([3L], store.Released);                 // handed back untouched, not lost
        Assert.Equal(new RelayResult(1, 1, 0), result);
    }

    [Fact]
    public async Task Back_off_doubles_per_attempt_and_is_capped()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Policy.BackoffAfter(1));
        Assert.Equal(TimeSpan.FromSeconds(10), Policy.BackoffAfter(2));
        Assert.Equal(TimeSpan.FromSeconds(20), Policy.BackoffAfter(3));
        Assert.Equal(TimeSpan.FromSeconds(40), Policy.BackoffAfter(4));
        Assert.Equal(TimeSpan.FromSeconds(40), Policy.BackoffAfter(30)); // capped, and no overflow
        await Task.CompletedTask;
    }

    [Fact]
    public async Task The_failed_row_is_scheduled_with_the_back_off_for_its_attempt_number()
    {
        var store = new FakeStore(Row(1, attempts: 1)); // this will be its second attempt
        var publisher = new FakePublisher(_ => new EventPublishException("down", false));

        await new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(10), store.Failed[0].RetryAfter);
    }

    [Fact]
    public async Task A_row_that_fails_its_last_allowed_attempt_is_dead_lettered_and_the_relay_moves_on()
    {
        var store = new FakeStore(Row(1, attempts: 2), Row(2));   // MaxAttempts is 3: this is attempt 3
        var publisher = new FakePublisher(e => e.Subject.EndsWith("1".PadLeft(12, '0')) ? new EventPublishException("down", false) : null);

        var result = await new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None);

        Assert.True(store.Failed[0].Dead);
        Assert.Equal([2L], store.Published);          // the next row still went
        Assert.Equal(new RelayResult(1, 0, 1), result);
    }

    [Fact]
    public async Task A_permanent_rejection_is_dead_lettered_at_once_not_retried()
    {
        var store = new FakeStore(Row(1));
        var publisher = new FakePublisher(_ => new EventPublishException("rejected", isPermanent: true));

        var result = await new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None);

        Assert.True(store.Failed[0].Dead);
        Assert.Equal(new RelayResult(0, 0, 1), result);
    }

    [Fact]
    public async Task A_row_whose_payload_can_never_be_built_is_dead_lettered_without_calling_the_consumer()
    {
        var store = new FakeStore(Row(1, payload: "not json"), Row(2));
        var publisher = new FakePublisher(_ => null);

        var result = await new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None);

        Assert.Equal([2L], publisher.Attempted);       // the poison row never reached the network
        Assert.True(store.Failed[0].Dead);
        Assert.Equal([2L], store.Published);
        Assert.Equal(new RelayResult(1, 0, 1), result);
    }

    [Fact]
    public async Task An_unexpected_exception_is_not_swallowed_as_a_delivery_failure()
    {
        var store = new FakeStore(Row(1));
        var publisher = new FakePublisher(_ => new InvalidOperationException("a bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new RelayOutboxUseCase(store, publisher, Policy).ExecuteAsync(CancellationToken.None));

        Assert.Empty(store.Failed);   // a bug is not retried into the dead letters; the host logs it and tries the next pass
    }
}
