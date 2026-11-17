namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// The identity of one user attempt to create something (ADR 0077). A dialog starts an attempt the first time its user presses save and keeps it while that save is being retried (the answer was lost, the
/// network dropped); the attempt ends when a save succeeds. The idempotency handler turns it into the <c>Idempotency-Key</c> header, so a retried save is answered with the first answer instead of creating a
/// second record, while two deliberate saves of the same thing (the same vitals recorded twice on purpose) are two attempts and two records.
/// </summary>
public sealed class IdempotencyAttempt
{
    /// <summary>The current attempt's id, or <c>null</c> when no save is in progress.</summary>
    public string? Current { get; private set; }

    /// <summary>Starts an attempt unless one is already in progress (a retry keeps the one it has).</summary>
    public void BeginIfNone() => Current ??= Guid.NewGuid().ToString("N");

    /// <summary>Ends the attempt: the save succeeded, so the next one is a new request.</summary>
    public void Clear() => Current = null;
}
