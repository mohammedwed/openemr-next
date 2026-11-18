namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// A window on a record's last-changed stamp: <paramref name="From"/> is inclusive, <paramref name="Until"/> exclusive, and either may be open. The stamps are the server's local wall-clock times, so the window is too.
/// Built from a FHIR <c>_lastUpdated</c> search (ADR 0079).
/// </summary>
public sealed record UpdatedWindow(DateTime? From, DateTime? Until)
{
    /// <summary>Whether a stamp is inside. A record with no stamp is never inside a window: it cannot be said to have changed when.</summary>
    public bool Contains(DateTime? stamp) =>
        stamp is { } s && (From is null || s >= From) && (Until is null || s < Until);

    /// <summary>Both windows at once: a stamp has to be in each (a range given as two parameters).</summary>
    public UpdatedWindow Intersect(UpdatedWindow other) => new(Later(From, other.From), Earlier(Until, other.Until));

    private static DateTime? Later(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a > b ? a : b;

    private static DateTime? Earlier(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a < b ? a : b;
}
