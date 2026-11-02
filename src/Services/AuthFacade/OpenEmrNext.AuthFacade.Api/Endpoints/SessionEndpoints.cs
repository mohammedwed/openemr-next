using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenEmrNext.AuthFacade.Api.Audit;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// <c>POST /sessions/revoke-all</c> and <c>POST /sessions/revoke/{subject}</c>: the two halves of the gap
/// ADR 0028/0040 both named — "force-ending a session from outside the browser that holds it (an admin
/// action, or a stolen-cookie response)". The first is self-service, the second is the admin action (ADR
/// 0045), gated by a config-driven allowlist (<see cref="AdminOptions"/>) since AuthFacade has no broader
/// authorization model to check against yet.
/// </summary>
public static class SessionEndpoints
{
    /// <summary>Maps <c>POST /sessions/revoke-all</c> and <c>POST /sessions/revoke/{subject}</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/sessions/revoke-all", HandleRevokeAllAsync)
            .RequireAuthorization()
            .ExcludeFromDescription();

        app.MapPost("/sessions/revoke/{subject}", HandleAdminRevokeAsync)
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

    /// <summary>
    /// Deletes every session row for <paramref name="subject"/> — an arbitrary other user, not the caller
    /// — if the caller's own subject appears in <see cref="AdminOptions.Subjects"/>. A caller not on that
    /// list gets 403, the same as an unauthenticated caller gets 401 from <c>RequireAuthorization()</c>
    /// above: this endpoint makes exactly one authorization decision, and it is this allowlist check, not
    /// anything broader. If the admin happens to be revoking their own subject, their own current cookie is
    /// signed out too, the same immediate-effect reasoning <see cref="HandleRevokeAllAsync"/> already uses.
    /// </summary>
    private static async Task<IResult> HandleAdminRevokeAsync(
        HttpContext context, string subject, AuthFacadeDbContext db, IOptions<AdminOptions> adminOptions, ILogger<AuditLog> auditLog)
    {
        var callerSubject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(callerSubject) || !adminOptions.Value.Subjects.Contains(callerSubject))
        {
            return Results.Forbid();
        }

        var sessions = await db.Sessions.Where(s => s.Subject == subject).ToListAsync();
        db.Sessions.RemoveRange(sessions);
        await db.SaveChangesAsync();

        auditLog.AdminRevokedSessions(callerSubject, subject);

        if (callerSubject == subject)
        {
            await context.SignOutAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme);
        }

        return Results.NoContent();
    }
}
