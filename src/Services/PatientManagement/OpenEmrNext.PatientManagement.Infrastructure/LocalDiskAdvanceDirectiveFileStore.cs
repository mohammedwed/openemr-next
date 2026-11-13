using OpenEmrNext.PatientManagement.Application.Ports;

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

    public Task<byte[]> ReadAsync(string url, CancellationToken ct)
    {
        const string prefix = "file://";
        var filePath = url.StartsWith(prefix, StringComparison.Ordinal) ? url[prefix.Length..] : url;
        return File.ReadAllBytesAsync(filePath, ct);
    }
}
