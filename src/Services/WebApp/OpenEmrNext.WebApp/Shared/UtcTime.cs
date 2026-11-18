namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// The WebApp's one rule for time: it is UTC. A time typed into a dialog, a default taken from the clock, and a time shown on a page are
/// all UTC, so what a user enters is what is stored and what they see afterwards. The service stores the digits it is sent (the database
/// server's clock is UTC), and the browser's own zone never enters into it: before this, the encounter dialog proposed the browser's local
/// time and attached the browser's offset, the service converted it to UTC, and the page showed that UTC value, so a user in UTC+2 who entered
/// 06:30 saw 04:30.
/// </summary>
public static class UtcTime
{
    /// <summary>Today's date in UTC (not the browser's date, which can be a day ahead or behind it for some hours).</summary>
    public static DateTime Today => DateTime.UtcNow.Date;

    /// <summary>
    /// The value to send for a date or date and time the user picked: the same digits, with a zero offset, whatever zone the browser is in.
    /// <c>new DateTimeOffset(value)</c> on an unspecified time would attach the browser's own offset instead.
    /// </summary>
    public static DateTimeOffset ToWire(DateTime picked) =>
        new(DateTime.SpecifyKind(picked, DateTimeKind.Unspecified), TimeSpan.Zero);
}
