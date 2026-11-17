using System.Text.Json;

namespace OpenEmrNext.Gateway;

/// <summary>
/// The gateway's one per-user decision (ADR 0075): is this request signed in? It asks AuthFacade, which owns the revocable server-side session store (ADR 0043, 0044), by calling its <c>GET /api/v1/me</c> with only the
/// caller's cookie. A valid session answers with the user's OpenEMR uuid as <c>sub</c>.
/// </summary>
internal sealed class SessionVerifier(IHttpClientFactory httpClientFactory, ILogger<SessionVerifier> logger)
{
    public const string ClientName = "authfacade-session";

    /// <summary>The header carrying the verified user's uuid to the services (ADR 0075). Removed from every proxied request first, so only this check can set it.</summary>
    public const string VerifiedUserHeader = "X-Authenticated-User";

    /// <summary>
    /// The property of AuthFacade's <c>MeResponse</c> that holds the session's subject, the user's OpenEMR uuid. It is <b>"subject"</b>, not the JWT claim's name "sub": a test pins this against AuthFacade's published
    /// contract (<c>AuthFacade.json</c>), because a wrong guess here makes every real session look signed out.
    /// </summary>
    public const string SubjectProperty = "subject";

    public async Task<SessionResult> VerifyAsync(HttpContext context, CancellationToken ct)
    {
        // No cookie at all is not worth a call.
        if (!context.Request.Headers.TryGetValue("Cookie", out var cookie) || string.IsNullOrWhiteSpace(cookie))
        {
            return SessionResult.NotSignedIn;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/me");
            request.Headers.TryAddWithoutValidation("Cookie", cookie.ToString());
            // AuthFacade reads the original scheme and address from these (ADR 0039), the same as for any request this gateway forwards to it.
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
            if (context.Connection.RemoteIpAddress is { } address)
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", address.ToString());
            }

            using var response = await httpClientFactory.CreateClient(ClientName).SendAsync(request, ct);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                return SessionResult.NotSignedIn;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AuthFacade answered the session check with {Status}; failing closed.", (int)response.StatusCode);
                return SessionResult.Unavailable;
            }

            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (body.RootElement.ValueKind == JsonValueKind.Object
                && body.RootElement.TryGetProperty(SubjectProperty, out var sub)
                && sub.ValueKind == JsonValueKind.String
                && Guid.TryParse(sub.GetString(), out var uuid)
                && uuid != Guid.Empty)
            {
                return SessionResult.SignedIn(uuid);
            }

            // A session whose subject is not an OpenEMR user uuid cannot be tied to a user, so it is not accepted.
            return SessionResult.NotSignedIn;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (ex is TaskCanceledException && ct.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(ex, "The session check against AuthFacade failed; failing closed.");
            return SessionResult.Unavailable;
        }
    }
}

internal readonly record struct SessionResult(SessionOutcome Outcome, Guid UserUuid)
{
    public static SessionResult NotSignedIn => new(SessionOutcome.NotSignedIn, Guid.Empty);

    public static SessionResult Unavailable => new(SessionOutcome.Unavailable, Guid.Empty);

    public static SessionResult SignedIn(Guid uuid) => new(SessionOutcome.SignedIn, uuid);
}

internal enum SessionOutcome { SignedIn, NotSignedIn, Unavailable }

/// <summary>Which paths need a signed-in user, derived from the same route patterns the proxy uses so the two cannot drift apart.</summary>
internal sealed class ProtectedPaths(IEnumerable<string> routePatterns)
{
    private const string CatchAll = "/{**catch-all}";

    private readonly string[] _prefixes = routePatterns
        .Select(p => p.EndsWith(CatchAll, StringComparison.Ordinal) ? p[..^CatchAll.Length] : p)
        .ToArray();

    /// <summary>True for a prefix itself and anything under it, compared case-insensitively as ASP.NET Core routing does.</summary>
    public bool Requires(PathString path) =>
        _prefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}

internal static class SessionIdentityExtensions
{
    public const string UserItem = "OpenEmrNext.VerifiedUser";

    /// <summary>
    /// Rejects an unauthenticated request to a protected path before it is proxied (401), fails closed when AuthFacade cannot say (503), and otherwise remembers the verified user for the proxy transform to forward.
    /// </summary>
    public static IApplicationBuilder UseSessionIdentity(this IApplicationBuilder app, ProtectedPaths protectedPaths) =>
        app.Use(async (context, next) =>
        {
            if (!protectedPaths.Requires(context.Request.Path))
            {
                await next();
                return;
            }

            var result = await context.RequestServices.GetRequiredService<SessionVerifier>().VerifyAsync(context, context.RequestAborted);
            switch (result.Outcome)
            {
                case SessionOutcome.SignedIn:
                    context.Items[UserItem] = result.UserUuid;
                    await next();
                    return;

                case SessionOutcome.NotSignedIn:
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { title = "Sign-in is required.", status = 401, detail = "Sign in with OpenEMR, then try again." });
                    return;

                default:
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    context.Response.Headers.RetryAfter = "5";
                    await context.Response.WriteAsJsonAsync(new { title = "Sign-in could not be checked.", status = 503, detail = "The sign-in service is unavailable. Nothing was done; try again in a moment." });
                    return;
            }
        });
}
