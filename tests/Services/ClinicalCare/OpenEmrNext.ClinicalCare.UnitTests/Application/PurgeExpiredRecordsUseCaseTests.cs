using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

/// <summary>Retention (finding R-10): a backlog is cleared in many short batches, and the loop always stops.</summary>
public class PurgeExpiredRecordsUseCaseTests
{
    private static readonly RetentionCutoffs Cutoffs = new(30, 30, 365);

    private sealed class ScriptedStore(params PurgeResult[] batches) : IRetentionStore
    {
        public int Calls { get; private set; }
        public RetentionCutoffs? LastCutoffs { get; private set; }
        public int? LastBatchSize { get; private set; }

        public Task<PurgeResult> PurgeBatchAsync(RetentionCutoffs cutoffs, int batchSize, CancellationToken ct)
        {
            LastCutoffs = cutoffs;
            LastBatchSize = batchSize;
            return Task.FromResult(Calls < batches.Length ? batches[Calls++] : (Calls++, PurgeResult.Nothing).Item2);
        }
    }

    [Fact]
    public async Task Nothing_due_is_one_batch_and_zero_removed()
    {
        var store = new ScriptedStore(PurgeResult.Nothing);

        var result = await new PurgeExpiredRecordsUseCase(store, Cutoffs, 100).ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, store.Calls);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task A_backlog_is_cleared_in_repeated_batches_until_every_table_returns_less_than_a_full_one()
    {
        var store = new ScriptedStore(new PurgeResult(100, 0, 40), new PurgeResult(100, 0, 0), new PurgeResult(30, 0, 0));

        var result = await new PurgeExpiredRecordsUseCase(store, Cutoffs, 100).ExecuteAsync(CancellationToken.None);

        Assert.Equal(3, store.Calls);
        Assert.Equal(new PurgeResult(230, 0, 40), result);
    }

    [Fact]
    public async Task One_table_with_more_to_do_keeps_the_loop_going_even_if_the_others_are_done()
    {
        var store = new ScriptedStore(new PurgeResult(0, 100, 0), new PurgeResult(0, 5, 0));

        await new PurgeExpiredRecordsUseCase(store, Cutoffs, 100).ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, store.Calls);
    }

    [Fact]
    public async Task A_store_that_always_reports_a_full_batch_cannot_spin_forever()
    {
        var always = Enumerable.Repeat(new PurgeResult(100, 100, 100), 500).ToArray();
        var store = new ScriptedStore(always);

        await new PurgeExpiredRecordsUseCase(store, Cutoffs, 100).ExecuteAsync(CancellationToken.None);

        Assert.Equal(PurgeExpiredRecordsUseCase.MaxBatchesPerRun, store.Calls);
    }

    [Fact]
    public async Task The_configured_cutoffs_and_batch_size_reach_the_store_unchanged()
    {
        var store = new ScriptedStore(PurgeResult.Nothing);

        await new PurgeExpiredRecordsUseCase(store, new RetentionCutoffs(7, 8, 9), 250).ExecuteAsync(CancellationToken.None);

        Assert.Equal(new RetentionCutoffs(7, 8, 9), store.LastCutoffs);
        Assert.Equal(250, store.LastBatchSize);
    }
}
