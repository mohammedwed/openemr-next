using OpenEmrNext.PatientManagement.Infrastructure;

namespace OpenEmrNext.PatientManagement.UnitTests.Persistence;

public class LocalDiskAdvanceDirectiveFileStoreTests : IDisposable
{
    private readonly string repositoryRoot = Path.Combine(Path.GetTempPath(), $"advance-directives-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(repositoryRoot))
            Directory.Delete(repositoryRoot, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_then_ReadAsync_round_trips_the_exact_bytes()
    {
        var store = new LocalDiskAdvanceDirectiveFileStore(repositoryRoot);
        byte[] content = [1, 2, 3, 4, 5];

        var url = await store.SaveAsync(pid: 1, Guid.NewGuid(), content, CancellationToken.None);
        var readBack = await store.ReadAsync(url, CancellationToken.None);

        Assert.Equal(content, readBack);
    }

    [Fact]
    public async Task ReadAsync_rejects_a_url_that_escapes_the_repository_root()
    {
        var store = new LocalDiskAdvanceDirectiveFileStore(repositoryRoot);
        var outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllBytesAsync(outsidePath, [9, 9, 9]);

        try
        {
            var traversalUrl = "file://" + Path.Combine(repositoryRoot, "..", Path.GetFileName(outsidePath));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => store.ReadAsync(traversalUrl, CancellationToken.None));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task ReadAsync_rejects_a_url_with_no_file_scheme_that_still_escapes_the_root()
    {
        var store = new LocalDiskAdvanceDirectiveFileStore(repositoryRoot);
        var outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllBytesAsync(outsidePath, [9, 9, 9]);

        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => store.ReadAsync(outsidePath, CancellationToken.None));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task DeleteAsync_removes_a_file_a_prior_SaveAsync_wrote()
    {
        var store = new LocalDiskAdvanceDirectiveFileStore(repositoryRoot);
        var url = await store.SaveAsync(pid: 1, Guid.NewGuid(), [1, 2, 3], CancellationToken.None);

        await store.DeleteAsync(url, CancellationToken.None);

        await Assert.ThrowsAnyAsync<IOException>(() => store.ReadAsync(url, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_is_a_no_op_when_the_file_is_already_gone()
    {
        var store = new LocalDiskAdvanceDirectiveFileStore(repositoryRoot);
        var url = await store.SaveAsync(pid: 1, Guid.NewGuid(), [1, 2, 3], CancellationToken.None);
        await store.DeleteAsync(url, CancellationToken.None);

        // Deleting an already-deleted file must not throw -- the caller's own best-effort cleanup
        // (EfAdvanceDirectiveRepository.AddDocumentAsync) must never itself raise a second exception.
        await store.DeleteAsync(url, CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_rejects_a_url_that_escapes_the_repository_root()
    {
        var store = new LocalDiskAdvanceDirectiveFileStore(repositoryRoot);
        var outsidePath = Path.Combine(Path.GetTempPath(), $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllBytesAsync(outsidePath, [9, 9, 9]);

        try
        {
            var traversalUrl = "file://" + Path.Combine(repositoryRoot, "..", Path.GetFileName(outsidePath));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => store.DeleteAsync(traversalUrl, CancellationToken.None));
            Assert.True(File.Exists(outsidePath), "A rejected delete must never touch the file outside the root.");
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }
}
