namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>The answer a creating request gave, kept so a repeat of it can be answered the same way.</summary>
public sealed record StoredResponse(int StatusCode, string? ContentType, string? Location, string? Body);

/// <summary>What happened when a request tried to take its idempotency key.</summary>
public enum ClaimOutcome
{
    /// <summary>The key was free (or its earlier claim had expired): this request owns it and must report back with <see cref="IIdempotencyStore.CompleteAsync"/> or <see cref="IIdempotencyStore.ReleaseAsync"/>.</summary>
    Claimed,

    /// <summary>An earlier request with the same key and the same content already finished: its answer is in <see cref="ClaimResult.Stored"/>.</summary>
    Replay,

    /// <summary>An earlier request with this key is still running (within the lease).</summary>
    InProgress,

    /// <summary>The key was used before for a different request.</summary>
    Mismatch
}

/// <param name="Token">Proves ownership of a <see cref="ClaimOutcome.Claimed"/> key; hand it back to complete or release.</param>
public sealed record ClaimResult(ClaimOutcome Outcome, Guid Token = default, StoredResponse? Stored = null);

/// <summary>
/// Remembers the answers of creating requests by (user, key) for a window, so a retried request does not create a second record (API-5, ADR 0084). A port so the table behind it stays in one adapter; the table is
/// this service's own.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Takes the key for this request, or says why it cannot. Atomic: of any number of simultaneous requests with one key, exactly one is <see cref="ClaimOutcome.Claimed"/>.</summary>
    Task<ClaimResult> ClaimAsync(long actorId, string key, string fingerprint, DateTime nowUtc, CancellationToken ct);

    /// <summary>Records the answer under the claim, so repeats get it. Does nothing if the claim has since been taken over.</summary>
    Task CompleteAsync(long actorId, string key, Guid token, StoredResponse response, DateTime nowUtc, CancellationToken ct);

    /// <summary>Gives the key up without an answer (the request failed), so a corrected retry can use it. Does nothing if the claim has since been taken over.</summary>
    Task ReleaseAsync(long actorId, string key, Guid token, CancellationToken ct);
}
