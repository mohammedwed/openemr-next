namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// Ports <c>Document::createDocument()</c>'s local-disk branch exactly: one subdirectory per patient
/// under <paramref name="repositoryRoot"/> -- effectively a repository-root/pid path, same shape as
/// legacy's own default <c>calculateStoragePath()</c> for a patient document with no higher-level path
/// -- and a UUID-named file with no extension inside it. <paramref name="repositoryRoot"/> should point
/// at the same path legacy's own install resolves <c>$GLOBALS['oer_config']['documents']['repopath']</c>
/// to (<c>sites/default/documents/</c>) -- normally via a shared volume -- so files this service writes
/// are visible to legacy's own document browser too.
/// </summary>
public sealed class LocalDiskAdvanceDirectiveFileStore(string repositoryRoot) : IAdvanceDirectiveFileStore
{
    public async Task<string> SaveAsync(long pid, Guid driveUuid, byte[] content, CancellationToken ct)
    {
        var directory = Path.Combine(repositoryRoot, pid.ToString());
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, driveUuid.ToString());
        await File.WriteAllBytesAsync(filePath, content, ct);

        return "file://" + filePath;
    }

    /// <exception cref="UnauthorizedAccessException"><paramref name="url"/> resolves to a path outside
    /// <c>repositoryRoot</c> -- defense in depth, not a path <see cref="SaveAsync"/> can ever produce
    /// itself (its own <c>pid</c>/<c>driveUuid</c> are a <see cref="long"/> and a <see cref="Guid"/>,
    /// neither able to carry a path-traversal sequence), but nothing structurally stops a future
    /// caller from handing this a <paramref name="url"/> it didn't get from this store.</exception>
    public Task<byte[]> ReadAsync(string url, CancellationToken ct) =>
        File.ReadAllBytesAsync(ResolveContainedPath(url), ct);

    /// <summary>
    /// Deletes the file a <see cref="SaveAsync"/> call that's about to be rolled back already wrote --
    /// the compensating half of <c>EfAdvanceDirectiveRepository.AddDocumentAsync</c>'s write-then-insert
    /// ordering: the file write and the <c>documents</c> row insert aren't atomic across disk and
    /// database, so a failed insert needs this to avoid leaving an orphan behind. A no-op if the file
    /// is already gone -- the caller's own best-effort cleanup should never itself be the reason a
    /// failure gets masked.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Same defense-in-depth check as <see cref="ReadAsync"/>.</exception>
    public Task DeleteAsync(string url, CancellationToken ct)
    {
        var path = ResolveContainedPath(url);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    /// <summary>Resolves <paramref name="url"/> to an absolute path and verifies it stays under
    /// <c>repositoryRoot</c> -- shared by <see cref="ReadAsync"/> and <see cref="DeleteAsync"/>, the
    /// two operations that take back a caller-supplied path rather than building one themselves.</summary>
    private string ResolveContainedPath(string url)
    {
        const string prefix = "file://";
        var filePath = url.StartsWith(prefix, StringComparison.Ordinal) ? url[prefix.Length..] : url;

        var resolvedRoot = Path.GetFullPath(repositoryRoot);
        var resolvedPath = Path.GetFullPath(filePath, resolvedRoot);
        if (!resolvedPath.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Refusing to touch a path outside the configured repository root: '{url}'.");

        return resolvedPath;
    }
}
