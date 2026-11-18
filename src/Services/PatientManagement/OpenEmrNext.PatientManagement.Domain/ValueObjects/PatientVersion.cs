using System.Security.Cryptography;
using System.Text;

namespace OpenEmrNext.PatientManagement.Domain.ValueObjects;

/// <summary>
/// The version of the part of a patient this service owns and edits (ADR 0081): a hash of those columns' values. <c>patient_data</c> has no change stamp the database maintains (its <c>date</c> column is set by the writer),
/// and 130 other columns that other writers change all the time, so a stamp could not be trusted and would report a conflict whenever anyone changed a phone number. A hash of the owned values changes exactly when something
/// this service would overwrite changes. The tag is <c>"p-</c> and 32 hex digits <c>"</c>; the prefix means a tag from another kind of record can never match.
/// </summary>
public readonly record struct PatientVersion
{
    private const string Prefix = "p-";
    private const int HexLength = 32;

    private readonly string? _hex;

    private PatientVersion(string hex) => _hex = hex;

    /// <summary>The version of these values, in this order. A <c>null</c> part and an empty one are different values, and the parts are length-prefixed so no two sets can collide by sliding a character across a boundary.</summary>
    public static PatientVersion Of(IEnumerable<string?> parts)
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
        return new PatientVersion(Convert.ToHexString(hash, 0, HexLength / 2).ToLowerInvariant());
    }

    /// <summary>The strong <c>ETag</c> value, quoted.</summary>
    public string ToETag() => $"\"{Prefix}{_hex ?? throw new InvalidOperationException("A default PatientVersion has no value.")}\"";

    /// <summary>The tags in an <c>If-Match</c> value that are a patient version. A tag of any other shape is skipped, so it can never match.</summary>
    public static IReadOnlyList<string> ParseIfMatch(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return [];

        var tags = new List<string>();
        foreach (var part in headerValue.Split(','))
        {
            var tag = part.Trim();
            if (tag.Length != HexLength + Prefix.Length + 2 || tag[0] != '"' || tag[^1] != '"' || !tag.AsSpan(1, Prefix.Length).SequenceEqual(Prefix))
                continue;

            var hex = tag.Substring(1 + Prefix.Length, HexLength);
            if (hex.All(Uri.IsHexDigit))
                tags.Add($"\"{Prefix}{hex.ToLowerInvariant()}\"");
        }

        return tags;
    }
}
