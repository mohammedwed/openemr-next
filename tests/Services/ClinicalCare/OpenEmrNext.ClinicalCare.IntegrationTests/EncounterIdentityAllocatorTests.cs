using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>CC-5 against the real shared <c>sequences</c> counter, as the production principal (LEG-12).</summary>
[Collection("ClinicalCareContainer")]
public class EncounterIdentityAllocatorTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CC_5_numbers_continue_the_shared_counter_one_at_a_time()
    {
        // ResetAsync seeds the counter at 1000, as if legacy had already issued up to there.
        await using var db = fixture.CreateDbContext();
        var allocator = new EfEncounterIdentityAllocator(db);

        var first = await allocator.AllocateAsync(CancellationToken.None);
        var second = await allocator.AllocateAsync(CancellationToken.None);

        Assert.Equal(1001, first.Number);
        Assert.Equal(1002, second.Number);
        Assert.NotEqual(first.Uuid, second.Uuid);
        Assert.Equal(1002L, await fixture.RootScalarAsync<long>("SELECT id FROM sequences"));
    }

    [Fact]
    public async Task CC_5_concurrent_allocation_never_hands_out_the_same_number_twice()
    {
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fixture.CreateDbContext();
            return (await new EfEncounterIdentityAllocator(db).AllocateAsync(CancellationToken.None)).Number;
        });

        var numbers = await Task.WhenAll(tasks);

        Assert.Equal(20, numbers.Distinct().Count());
        Assert.Equal(Enumerable.Range(1001, 20).Select(n => (long)n), numbers.Order());
    }

    [Fact]
    public async Task CC_5_a_legacy_writer_using_the_same_idiom_interleaves_without_collision()
    {
        // What legacy's QueryUtils::generateId() (ADOdb GenID) does, run on its own connection between our calls.
        await using var db = fixture.CreateDbContext();
        var allocator = new EfEncounterIdentityAllocator(db);

        var ours1 = await allocator.AllocateAsync(CancellationToken.None);
        await fixture.RootExecuteAsync("UPDATE sequences SET id = LAST_INSERT_ID(id + 1)");
        var ours2 = await allocator.AllocateAsync(CancellationToken.None);

        Assert.Equal(1001, ours1.Number);
        Assert.Equal(1003, ours2.Number);
    }
}
