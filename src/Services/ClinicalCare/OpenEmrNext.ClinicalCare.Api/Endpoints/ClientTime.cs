namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// What a date the client sent means (review finding R-11). Legacy's <c>datetime</c> columns hold <b>server-local time</b> with no
/// zone, and the domain compares and stores exactly the digits it is given. A client may nevertheless send a zone (<c>Z</c>,
/// <c>+02:00</c>); before this, the zone was dropped and the clock digits stored as if they were local, so <c>08:30Z</c> became 08:30
/// local, which is a different instant.
/// <para>
/// The rule, applied once at the edge where the wire format is parsed: a date **with** a zone is an instant and is converted to the
/// server's local time; a date **without** one is already the server's local time, as the API has always documented.
/// </para>
/// </summary>
internal static class ClientTime
{
    public static DateTime? ToServerLocal(DateTime? sent) => sent switch
    {
        null => null,
        // System.Text.Json gives Utc for "Z", Local (already converted) for an explicit offset, Unspecified for no zone at all.
        { Kind: DateTimeKind.Utc } utc => utc.ToLocalTime(),
        var other => other
    };
}
