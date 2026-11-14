namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// Where an advance directive's actual file bytes live. Legacy supports local disk (its default),
/// CouchDB, and a pluggable remote-storage hook for this; this interface covers the local-disk path
/// only -- the one real installs actually use, and the one that lets legacy's own document browser read
/// a file this service writes back, given a shared volume at the same repository root.
/// </summary>
/// <remarks>
/// Lives in <c>Infrastructure</c>, not <c>Application.Ports</c>, despite the name: nothing in
/// <c>Application</c> ever depends on it (confirmed by grep) -- only
/// <see cref="EfAdvanceDirectiveRepository"/> and <see cref="LocalDiskAdvanceDirectiveFileStore"/> do,
/// both adapters. It's an internal split between two pieces of one adapter (document metadata in
/// MariaDB, file bytes on disk), not a true hexagonal port a use case depends on. The composition root
/// (<c>PatientManagementApiExtensions</c>) still wires <see cref="LocalDiskAdvanceDirectiveFileStore"/>
/// to it explicitly, the same as every other adapter registration -- that's a DI concern, not a reason
/// to keep the abstraction itself in <c>Application.Ports</c>.
/// </remarks>
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
