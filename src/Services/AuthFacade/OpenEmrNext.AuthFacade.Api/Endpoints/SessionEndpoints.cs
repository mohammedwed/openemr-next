using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// <c>POST /sessions/revoke-all</c>: the self-service response to a suspected stolen
/// <c>AuthFacadeSession</c> cookie (ADR 0044) — ends every session recorded for the caller's own subject,
/// not just the one making the request, since a copy an attacker holds is itself a separate, still-valid
/// session record the caller has no other way to reach. Requires the caller to already be signed in: there
/// is no unauthenticated way to say whose sessions to revoke, and revoking an arbitrary other user's
/// session would need an authorization model this project does not have yet (a known gap, see README).
/// </summary>
public static class SessionEndpoints
{
    /// <summary>Maps <c>POST /sessions/revoke-all</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sessions/revoke-all", HandleRevokeAllAsync)
            .RequireAuthorization()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// Deletes every <see cref="AuthFacadeDbContext.Sessions"/> row for the caller's own subject —
    /// including the one the caller is using right now — then signs this request's own cookie out too, so
    /// this browser stops sending it immediately instead of only failing on its next use. Does not, and
    /// cannot, reach OpenEMR's own session or the underlying access/refresh token: OpenEMR exposes no
    /// server-to-server revocation endpoint (confirmed against its own OIDC discovery document and source),
    /// only the browser-driven RP-initiated logout <c>/logout</c> already uses (ADR 0040).
    /// </summary>
    private static async Task<IResult> HandleRevokeAllAsync(HttpContext context, AuthFacadeDbContext db)
    {
        var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!string.IsNullOrEmpty(subject))
        {
            var sessions = await db.Sessions.Where(s => s.Subject == subject).ToListAsync();
            db.Sessions.RemoveRange(sessions);
            await db.SaveChangesAsync();
        }

        await context.SignOutAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme);
        return Results.NoContent();
    }
}
