namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// Where an advance directive's actual file bytes live. Legacy supports local disk (its default),
/// CouchDB, and a pluggable remote-storage hook for this; this port covers the local-disk path only --
/// the one real installs actually use, and the one that lets legacy's own document browser read a file
/// this service writes back, given a shared volume at the same repository root.
/// </summary>
public interface IAdvanceDirectiveFileStore
{
    /// <summary>
    /// Writes <paramref name="content"/> under the patient's own subdirectory, named by
    /// <paramref name="driveUuid"/> (no extension), matching
    /// <c>Document::calculateStoragePath()</c>/<c>createDocument()</c> exactly. Returns the
    /// <c>file://</c>-prefixed absolute path legacy itself stores in <c>documents.url</c>.
    /// </summary>
    Task<string> SaveAsync(long pid, Guid driveUuid, byte[] content, CancellationToken ct);

    /// <summary>Reads back the bytes at a <c>file://</c> url previously returned by <see cref="SaveAsync"/>.</summary>
    Task<byte[]> ReadAsync(string url, CancellationToken ct);
}
