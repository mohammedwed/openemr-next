using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenEmrNext.PatientManagement.Application.Ports;

/// <summary>
/// The rules of the <c>Idempotency-Key</c> header on creating requests (API-5, ADR 0084). A caller that sends the same key with the same request gets the first answer back instead of a second record; the same key
/// with a different request is refused. Kept as plain constants and pure functions so the middleware, the store and the tests share one definition.
/// </summary>
public static partial class Idempotency
{
    public const string HeaderName = "Idempotency-Key";

    /// <summary>The longest key accepted; the column is this wide.</summary>
    public const int MaxKeyLength = 64;

    /// <summary>How long an answer is kept for replay. After it a key is as new as any other.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>How long a request may hold a key before another may take it over. A request that dies mid-flight leaves its claim behind; this is how long that blocks a retry.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    /// <summary>The largest answer stored for replay. A creating request answers with identifiers, not documents; one bigger than this is passed through unprotected, not truncated.</summary>
    public const int MaxStoredBodyBytes = 64 * 1024;

    /// <summary>The largest request body read to fingerprint it. A bigger one is refused (413) rather than buffered whole.</summary>
    public const int MaxRequestBodyBytes = 1024 * 1024;

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex KeyPattern();

    /// <summary>A key is 1 to 64 characters of letters, digits and <c>_ . : -</c>: enough for a UUID or a counter, nothing that needs escaping anywhere it is stored or logged.</summary>
    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>
    /// What "the same request" means: the method, the path with its query, and the body bytes. A hash, so the table holds no request text. The user is part of the key's scope, not of this value.
    /// </summary>
    public static string Fingerprint(string method, string pathAndQuery, ReadOnlySpan<byte> body)
    {
        var head = Encoding.UTF8.GetBytes($"{method.ToUpperInvariant()}\n{pathAndQuery}\n");
        var all = new byte[head.Length + body.Length];
        head.CopyTo(all, 0);
        body.CopyTo(all.AsSpan(head.Length));
        return Convert.ToHexString(SHA256.HashData(all)).ToLowerInvariant();
    }
}
