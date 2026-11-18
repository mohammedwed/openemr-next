namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// How a problem's or an allergy's dates cross the wire, in one place. The API returns a date with no zone (it is the server's local wall-clock time, like legacy's column), the
/// generated client reads it as <b>browser-local</b>, and on the way back sends it with an offset that the API converts to <b>server</b> time. Sending the browser's own offset
/// would therefore shift a date whenever the two zones differ, and the quick Resolve and Reopen buttons re-send a row's begin date every time they are used.
/// </summary>
/// <remarks>
/// So a date goes back as the same wall-clock time at offset zero. That is exact when the API runs in UTC, which the container default is, and it keeps any time of day a legacy row
/// already has (the issue screen can save one) instead of silently dropping it. A deployment whose API zone is not UTC still needs the real fix: a zone-less date on the contract
/// (docs/patient-view-pending-decisions.md, item 9).
/// </remarks>
public static class IssueWire
{
    /// <summary>The wall-clock date and time of a value read from the API, as the user should see it.</summary>
    public static (DateTime? Date, TimeSpan TimeOfDay) FromWire(DateTimeOffset? value) =>
        value is { } v ? (v.Date, v.TimeOfDay) : (null, TimeSpan.Zero);

    /// <summary>The value to send back for a calendar date the user picked, keeping <paramref name="timeOfDay"/> (what the stored row already had, or midnight for a new date).</summary>
    public static DateTimeOffset? ToWire(DateTime? date, TimeSpan timeOfDay) =>
        date is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d.Date + timeOfDay, DateTimeKind.Unspecified), TimeSpan.Zero) : null;

    /// <summary>The end date the quick Resolve action sets: today, as a calendar date.</summary>
    public static DateTimeOffset Today() => ToWire(UtcTime.Today, TimeSpan.Zero)!.Value;
}
