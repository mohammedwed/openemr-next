using System.Globalization;

namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The optimistic-concurrency token for a row shared with legacy writers (LEG-10, ADR 0068): the row's own last-update stamp
/// (<c>form_encounter.last_update</c>, <c>form_vitals.last_updated</c>), which the database sets on every real change by every writer.
/// Its wire form is a strong <c>ETag</c>, and it is opaque to the client.
/// </summary>
/// <remarks>
/// The stamp has whole-second precision (ADR 0068), so two writes inside the second the client read in are indistinguishable. That
/// residual window is accepted and documented; it is why this is the best token available, not a perfect one.
/// <para>
/// Equality is on the stamp alone: the connector returns <c>DateTime</c> with an unspecified kind and the value only ever compares to
/// another value read the same way.
/// </para>
/// </remarks>
/// <param name="Stamp">The row's last-update stamp, truncated to whole seconds.</param>
public readonly record struct ConcurrencyToken(DateTime Stamp)
{
    /// <summary>The token for a row last changed at <paramref name="stamp"/>. Sub-second parts are dropped: the column cannot hold them.</summary>
    public static ConcurrencyToken From(DateTime stamp) =>
        new(new DateTime(stamp.Ticks - stamp.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified));

    /// <summary>The strong entity tag: the stamp's tick count in hex, in double quotes (RFC 9110 section 8.8.3).</summary>
    public string ToETag() => $"\"{Stamp.Ticks:x}\"";

    /// <summary>
    /// Reads every well-formed entity tag in an <c>If-Match</c> header value (a comma-separated list is allowed). A weak tag
    /// (<c>W/"..."</c>) never matches under <c>If-Match</c>'s strong comparison, and anything unreadable is skipped, so neither can make a
    /// stale write succeed. An empty result means "nothing here can match", which the caller answers with 412.
    /// </summary>
    public static IReadOnlyList<ConcurrencyToken> ParseIfMatch(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return [];

        var tokens = new List<ConcurrencyToken>();
        foreach (var part in headerValue.Split(','))
        {
            var tag = part.Trim();
            if (tag.Length < 3 || tag[0] != '"' || tag[^1] != '"')
                continue;

            if (long.TryParse(tag.AsSpan(1, tag.Length - 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var ticks)
                && ticks is > 0 && ticks <= DateTime.MaxValue.Ticks && ticks % TimeSpan.TicksPerSecond == 0)
            {
                tokens.Add(new ConcurrencyToken(new DateTime(ticks, DateTimeKind.Unspecified)));
            }
        }

        return tokens;
    }
}
