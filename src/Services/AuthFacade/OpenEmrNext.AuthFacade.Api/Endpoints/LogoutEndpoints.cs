using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// <c>GET /logout</c>: ends the local session and, if OpenEMR actually issued one, OpenEMR's own session
/// too, via RP-initiated logout (ADR 0040). OpenEMR's <c>end_session_endpoint</c> requires the original
/// <c>id_token</c> as <c>id_token_hint</c> — not optional; a bare request without one is refused with
/// <c>invalid_request</c>, confirmed against the real server — so this always signs out locally first and
/// only redirects to OpenEMR when an <c>id_token</c> was actually captured at login (ADR 0027 predates
/// capturing it, so an older session has none).
/// </summary>
public static class LogoutEndpoints
{
    /// <summary>Maps <c>GET /logout</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapLogoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/logout", HandleLogoutAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// Always lands on <see cref="BrowserFlowOptions.DefaultReturnUrl"/> — unlike <c>/oidc/login</c>, logout
    /// has no caller-supplied <c>returnUrl</c> to honor: landing back on the page the user just signed out
    /// of would be the wrong default, not a convenience. That same fixed URL is what the seed registers as
    /// this client's <c>logout_redirect_uris</c> (ADR 0040) — OpenEMR only redirects back to a
    /// <c>post_logout_redirect_uri</c> that matches its registration byte for byte, so this cannot be a
    /// caller-supplied value the way <c>/oidc/login</c>'s own return URL is.
    /// </summary>
    private static async Task<IResult> HandleLogoutAsync(
        HttpContext context, IOptions<OpenEmrOptions> openEmrOptions, IOptions<BrowserFlowOptions> browserFlowOptions)
    {
        var idToken = await context.GetTokenAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, "id_token");
        await context.SignOutAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme);

        var returnUrl = browserFlowOptions.Value.DefaultReturnUrl;
        if (string.IsNullOrEmpty(idToken))
        {
            // Not signed in, or a session that predates capturing id_token (ADR 0040) -- nothing for
            // OpenEMR to end, and no id_token to send it even if there were.
            return Results.Redirect(returnUrl);
        }

        var issuer = openEmrOptions.Value.Issuer.TrimEnd('/');
        var endSessionUrl = QueryHelpers.AddQueryString($"{issuer}/logout", new Dictionary<string, string?>
        {
            ["id_token_hint"] = idToken,
            ["post_logout_redirect_uri"] = returnUrl
        });

        return Results.Redirect(endSessionUrl);
    }
}
