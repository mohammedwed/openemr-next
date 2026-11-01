using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenIddict.Client.AspNetCore;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// The OpenIddict-driven browser authorization-code flow against OpenEMR: <c>GET /oidc/login</c> starts it
/// with <see cref="Results.Challenge(AuthenticationProperties?, string[])"/>, and <c>/callback</c> — the
/// OpenIddict client's own redirection endpoint, reached via passthrough — completes it and establishes the
/// local session cookie. Reuses <see cref="ReturnUrlValidator"/> and <see cref="BrowserFlowOptions"/> from
/// the earlier hand-rolled flow (ADR 0024); the allow-list problem they solve is unchanged by which engine
/// drives the redirect.
/// </summary>
public static class OpenIddictClientEndpoints
{
    /// <summary>Maps <c>GET /oidc/login</c> and the OpenIddict client's passthrough callback at <c>/callback</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapOpenIddictClientEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/oidc/login", HandleLogin)
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Cast to Delegate: MapMethods otherwise binds this to the raw-RequestDelegate overload (ASP0016),
        // which discards the Task<IResult> return value and would always answer a bare 200.
        app.MapMethods("/callback", ["GET", "POST"], (Delegate)HandleCallbackAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>Resolves <paramref name="returnUrl"/> against the allow-list and starts the challenge.</summary>
    private static IResult HandleLogin(string? returnUrl, IOptions<BrowserFlowOptions> browserFlowOptions)
    {
        var resolvedReturnUrl = ReturnUrlValidator.Resolve(returnUrl, browserFlowOptions.Value);

        return Results.Challenge(
            new AuthenticationProperties { RedirectUri = resolvedReturnUrl },
            [OpenIddictClientAspNetCoreDefaults.AuthenticationScheme]);
    }

    /// <summary>
    /// Reads the result OpenIddict's client handler already produced for this redirect (it validated the
    /// code exchange, the <c>id_token</c>'s signature/issuer/audience, and its own state token's nonce —
    /// none of that is re-implemented here). On success, copies every claim into a new local-session
    /// identity, carries the real OpenEMR access/refresh token into the session cookie (see
    /// <see cref="OpenIddictTokenCapture.Capture"/>), and signs in with the cookie scheme; on failure,
    /// redirects to the resolved return URL (OpenIddict preserves <see cref="AuthenticationProperties.RedirectUri"/>
    /// from the challenge) with a generic error code, never OpenEMR's or OpenIddict's own error text.
    /// </summary>
    private static async Task<IResult> HandleCallbackAsync(HttpContext context, IOptions<BrowserFlowOptions> browserFlowOptions)
    {
        AuthenticateResult result;
        try
        {
            result = await context.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        }
        catch (Exception)
        {
            // The token exchange itself blew up before OpenIddict could produce a failed AuthenticateResult
            // at all -- e.g. the circuit breaker (ADR 0041) refusing the call outright because OpenEMR's
            // token endpoint is already known to be down. There is no AuthenticationProperties to recover a
            // returnUrl from here, so this falls back to the same default /oidc/login itself uses for a
            // returnUrl it cannot use.
            return Results.Redirect(QueryHelpers.AddQueryString(browserFlowOptions.Value.DefaultReturnUrl, "error", "login_failed"));
        }

        var returnUrl = result.Properties?.RedirectUri ?? browserFlowOptions.Value.DefaultReturnUrl;

        if (!result.Succeeded || result.Principal is not { Identity.IsAuthenticated: true } principal)
        {
            return Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "error", "login_failed"));
        }

        var identity = new ClaimsIdentity(principal.Claims, OpenIddictClientServiceCollectionExtensions.SessionScheme);
        await context.SignInAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, new ClaimsPrincipal(identity), OpenIddictTokenCapture.Capture(result));

        return Results.Redirect(returnUrl);
    }
}
