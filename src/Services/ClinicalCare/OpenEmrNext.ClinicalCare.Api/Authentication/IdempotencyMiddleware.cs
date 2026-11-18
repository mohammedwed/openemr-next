using Microsoft.Extensions.Primitives;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Api.Authentication;

/// <summary>
/// The <c>Idempotency-Key</c> header on creating requests (API-5, ADR 0077). A <c>POST</c> that carries the header runs once: a retry with the same key and the same request is answered with the first answer (marked
/// <c>Idempotent-Replayed: true</c>) and creates nothing; the same key with a different request is 422; the same key while the first is still running is 409. Without the header nothing changes, so existing callers
/// are unaffected.
/// </summary>
/// <remarks>
/// <para>Scoped to the verified user, so one user's key can never replay another's answer (it runs after <see cref="VerifiedUserMiddleware"/>). Only a 2xx answer is kept; a refused or failed request releases its key,
/// so a corrected retry can use the same one. An answer too large to store is passed through and its key released, never truncated.</para>
/// <para>Middleware, not a filter, for the reason the identity check is: the request is judged before its body is bound.</para>
/// </remarks>
internal sealed class IdempotencyMiddleware(RequestDelegate next, TimeProvider clock)
{
    public const string ReplayedHeader = "Idempotent-Replayed";

    public async Task InvokeAsync(HttpContext http, IIdempotencyStore store)
    {
        if (!HttpMethods.IsPost(http.Request.Method) || !http.Request.Headers.TryGetValue(Idempotency.HeaderName, out var offered))
        {
            await next(http);
            return;
        }

        if (offered.Count != 1 || !Idempotency.IsValidKey(offered[0]) || http.Items[VerifiedUserMiddleware.ItemKey] is not VerifiedActor actor)
        {
            await Problem(http, "Invalid Idempotency-Key.", $"Send one {Idempotency.HeaderName} of 1 to {Idempotency.MaxKeyLength} letters, digits or _ . : - (a UUID works).", StatusCodes.Status400BadRequest);
            return;
        }

        var key = offered[0]!;
        if (http.Request.ContentLength > Idempotency.MaxRequestBodyBytes)
        {
            await Problem(http, "Request too large.", "A request carrying an Idempotency-Key must be 1 MB or smaller.", StatusCodes.Status413PayloadTooLarge);
            return;
        }

        var body = await ReadBodyAsync(http);
        if (body is null)
        {
            await Problem(http, "Request too large.", "A request carrying an Idempotency-Key must be 1 MB or smaller.", StatusCodes.Status413PayloadTooLarge);
            return;
        }

        var fingerprint = Idempotency.Fingerprint(http.Request.Method, http.Request.Path + http.Request.QueryString, body);
        var claim = await store.ClaimAsync(actor.UserId, key, fingerprint, clock.GetUtcNow().UtcDateTime, http.RequestAborted);

        switch (claim.Outcome)
        {
            case ClaimOutcome.Mismatch:
                await Problem(http, "Idempotency-Key reused.", "This key was already used for a different request. Use a new key for a new request.", StatusCodes.Status422UnprocessableEntity);
                return;
            case ClaimOutcome.InProgress:
                http.Response.Headers.RetryAfter = "5";
                await Problem(http, "Request in progress.", "An earlier request with this key is still being processed. Wait a few seconds and send the same request again.", StatusCodes.Status409Conflict);
                return;
            case ClaimOutcome.Replay:
                await ReplayAsync(http, claim.Stored!);
                return;
        }

        await RunAndRecordAsync(http, store, actor.UserId, key, claim.Token);
    }

    private async Task RunAndRecordAsync(HttpContext http, IIdempotencyStore store, long actorId, string key, Guid token)
    {
        var original = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        var finished = false;
        try
        {
            await next(http);
            finished = true;
        }
        finally
        {
            http.Response.Body = original;

            // Decided here, once the answer is known. The store calls must finish even if the caller has gone, so they do not take the request's cancellation.
            if (finished && http.Response.StatusCode is >= 200 and < 300 && buffer.Length <= Idempotency.MaxStoredBodyBytes)
            {
                var stored = new StoredResponse(
                    http.Response.StatusCode, http.Response.ContentType, http.Response.Headers.Location.FirstOrDefault(),
                    buffer.Length == 0 ? null : System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length));
                await store.CompleteAsync(actorId, key, token, stored, clock.GetUtcNow().UtcDateTime, CancellationToken.None);
            }
            else
            {
                await store.ReleaseAsync(actorId, key, token, CancellationToken.None);
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original, http.RequestAborted);
    }

    private static async Task ReplayAsync(HttpContext http, StoredResponse stored)
    {
        http.Response.StatusCode = stored.StatusCode;
        http.Response.Headers[ReplayedHeader] = "true";
        if (stored.Location is not null)
            http.Response.Headers.Location = new StringValues(stored.Location);
        if (stored.ContentType is not null)
            http.Response.ContentType = stored.ContentType;
        if (stored.Body is not null)
            await http.Response.WriteAsync(stored.Body, System.Text.Encoding.UTF8);
    }

    /// <summary>The whole body, or <c>null</c> if it is over the limit (a chunked body has no declared length to check first).</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpContext http)
    {
        http.Request.EnableBuffering();
        using var all = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await http.Request.Body.ReadAsync(chunk, http.RequestAborted)) > 0)
        {
            all.Write(chunk, 0, read);
            if (all.Length > Idempotency.MaxRequestBodyBytes)
                return null;
        }

        http.Request.Body.Position = 0;
        return all.ToArray();
    }

    private static Task Problem(HttpContext http, string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status).ExecuteAsync(http);
}

internal static class IdempotencyExtensions
{
    /// <summary>Applies <see cref="IdempotencyMiddleware"/> to <c>/api/v1</c>, after the identity check it depends on.</summary>
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app) =>
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase), branch => branch.UseMiddleware<IdempotencyMiddleware>());
}
