using System.Security.Cryptography;
using System.Text;
using OpenEmrNext.WebApp.Shared;

namespace OpenEmrNext.WebApp.Resilience;

/// <summary>
/// Adds an <c>Idempotency-Key</c> to a creating <c>POST</c> while a user attempt is in progress (ADR 0077). The key is the attempt's id plus a short hash of the request's path and body, so the same attempt
/// sending the same request twice sends the same key (the service answers the second with the first answer), and an attempt whose user has since edited the form sends a different one (a different request is not
/// a replay). Nothing is added to a request that is not a <c>POST</c>, that has no attempt, or that already carries a key. A successful answer ends the attempt; anything else keeps it, because the retry is the point.
/// </summary>
internal sealed class IdempotencyKeyHandler(IdempotencyAttempt attempt) : DelegatingHandler
{
    public const string HeaderName = "Idempotency-Key";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var attemptId = attempt.Current;
        var applies = attemptId is not null && request.Method == HttpMethod.Post && !request.Headers.Contains(HeaderName) && !TakesNoKey(request);
        if (applies)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation(HeaderName, KeyFor(attemptId!, request.RequestUri?.PathAndQuery ?? "", body));
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (applies && response.IsSuccessStatusCode)
            attempt.Clear();

        return response;
    }

    /// <summary>
    /// A directive document upload carries its file as base64, far larger than a request can be fingerprinted at, so Patient Management takes no key there and refuses one (ADR 0084). An attempt that
    /// outlived a failed dialog must not tag it.
    /// </summary>
    private static bool TakesNoKey(HttpRequestMessage request) =>
        request.RequestUri?.AbsolutePath.EndsWith("/advance-directives/documents", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>32 characters of attempt id, a dash, and 16 of hash: 49 characters of letters, digits and a dash, inside the service's 64.</summary>
    internal static string KeyFor(string attemptId, string pathAndQuery, byte[] body)
    {
        var head = Encoding.UTF8.GetBytes(pathAndQuery + "\n");
        var all = new byte[head.Length + body.Length];
        head.CopyTo(all, 0);
        body.CopyTo(all, head.Length);
        return $"{attemptId}-{Convert.ToHexString(SHA256.HashData(all))[..16].ToLowerInvariant()}";
    }
}
