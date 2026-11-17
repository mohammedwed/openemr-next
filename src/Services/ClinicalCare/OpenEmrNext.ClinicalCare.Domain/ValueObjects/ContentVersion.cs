using System.Security.Cryptography;
using System.Text;

namespace OpenEmrNext.ClinicalCare.Domain.ValueObjects;

/// <summary>
/// The optimistic-concurrency token for a row that has <b>no usable stamp</b> (ADR 0072): a hash of the row's owned columns, sent as a strong <c>ETag</c> of its own shape (<c>"c-..."</c>), so it can never be mistaken for a
/// <see cref="ConcurrencyToken"/>'s. A change by any writer to any owned column changes it; a change to a column this service does not own does not, which is right, because that cannot affect what a correction writes.
/// </summary>
/// <remarks>
/// The hash is of a canonical text, not of the values joined: every part is length-prefixed and a missing part has its own marker, so two different rows can never share a canonical text by accident of concatenation
/// (<c>"ab","c"</c> versus <c>"a","bc"</c>, or a missing value versus an empty string). SHA-256, cut to 128 bits: this is a change detector, not a secret.
/// </remarks>
public readonly record struct ContentVersion
{
    private const string Prefix = "c-";
    private const int HexLength = 32;

    private readonly string? _hex;

    private ContentVersion(string hex) => _hex = hex;

    /// <summary>The version of a row with these parts, in this order. A <c>null</c> part is "absent" and is not the same as an empty one.</summary>
    public static ContentVersion Of(IEnumerable<string?> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var canonical = new StringBuilder();
        foreach (var part in parts)
        {
            if (part is null)
                canonical.Append("-;");
            else
                canonical.Append(part.Length).Append(':').Append(part).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return new ContentVersion(Convert.ToHexString(hash, 0, HexLength / 2).ToLowerInvariant());
    }

    /// <summary>The strong entity tag: <c>"c-</c> and 32 hex digits <c>"</c>.</summary>
    public string ToETag() => $"\"{Prefix}{_hex ?? throw new InvalidOperationException("A default ContentVersion has no value.")}\"";

    /// <summary>
    /// Reads every well-formed content tag in an <c>If-Match</c> header value (a comma-separated list is allowed). A weak tag never matches under <c>If-Match</c>'s strong comparison, a stamp's tag is not a content tag, and
    /// anything unreadable is skipped, so none of them can make a stale write succeed. An empty result means "nothing here can match", which the caller answers with 412.
    /// </summary>
    public static IReadOnlyList<ContentVersion> ParseIfMatch(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return [];

        var versions = new List<ContentVersion>();
        foreach (var part in headerValue.Split(','))
        {
            var tag = part.Trim();
            if (tag.Length != HexLength + Prefix.Length + 2 || tag[0] != '"' || tag[^1] != '"' || !tag.AsSpan(1, Prefix.Length).SequenceEqual(Prefix))
                continue;

            var hex = tag.Substring(1 + Prefix.Length, HexLength);
            if (hex.All(Uri.IsHexDigit))
                versions.Add(new ContentVersion(hex.ToLowerInvariant()));
        }

        return versions;
    }
}
