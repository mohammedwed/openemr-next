using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api.BrowserFlow;
using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// The browser authorization-code flow with PKCE (ADR 0024): <c>GET /login</c> starts it by redirecting to
/// OpenEMR's own <c>/authorize</c>, and <c>GET /callback</c> completes it. Unlike <c>/api/v1/connect/token</c>,
/// these are browser navigations, not an API a client calls for a JSON body, so they sit outside
/// <c>/api/v1</c> and the OpenAPI document, the same way <c>/health</c> does.
/// </summary>
public static class BrowserFlowEndpoints
{
    /// <summary>The scope requested in the authorization-code flow — the scope the client is seeded with (ADR 0005).</summary>
    private const string Scope = "openid profile email offline_access";

    /// <summary>Maps <c>GET /login</c> and <c>GET /callback</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapBrowserFlowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/login", HandleLogin)
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/callback", HandleCallbackAsync)
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// Starts the flow: resolves <paramref name="returnUrl"/> against the allow-list, generates <c>state</c>,
    /// <c>nonce</c> and a PKCE <c>code_verifier</c>, stashes them in the <c>authfacade_oidc</c> cookie, and
    /// redirects to OpenEMR's <c>/authorize</c> with the matching <c>code_challenge</c>.
    /// </summary>
    private static IResult HandleLogin(
        string? returnUrl,
        HttpContext context,
        IOptions<OpenEmrOptions> openEmrOptions,
        IOptions<BrowserFlowOptions> browserFlowOptions,
        OidcFlowCookieProtector flowProtector)
    {
        var resolvedReturnUrl = ReturnUrlValidator.Resolve(returnUrl, browserFlowOptions.Value);

        var state = Pkce.GenerateToken();
        var nonce = Pkce.GenerateToken();
        var codeVerifier = Pkce.GenerateToken();
        var codeChallenge = Pkce.ComputeChallenge(codeVerifier);

        var flowState = new OidcFlowState(state, nonce, codeVerifier, resolvedReturnUrl);
        context.Response.Cookies.Append(
            OidcFlowCookieProtector.CookieName,
            flowProtector.Protect(flowState),
            FlowCookieOptions(context));

        var openEmr = openEmrOptions.Value;
        // The browser must reach this URL itself, so it is built from Issuer (the publicly-reachable
        // address) and not BaseUrl (the container-internal address AuthFacade uses for server-to-server
        // calls) — the two differ inside Compose. Issuer already ends in "/oauth2/default".
        var authorizeUrl = QueryHelpers.AddQueryString(
            openEmr.Issuer.TrimEnd('/') + "/authorize",
            new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = openEmr.ClientId,
                ["redirect_uri"] = openEmr.RedirectUri,
                ["scope"] = Scope,
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256"
            });

        return Results.Redirect(authorizeUrl);
    }

    /// <summary>
    /// Completes the flow: checks <c>state</c> against the <c>authfacade_oidc</c> cookie, redeems the code,
    /// verifies the <c>id_token</c> and its <c>nonce</c>, and on success sets <c>authfacade_session</c> and
    /// redirects to the stashed <c>returnUrl</c>. A failure redirects to the same place with a generic
    /// <c>error</c> code (<see cref="BrowserFlowErrorCodes"/>) — never OpenEMR's own error text.
    /// </summary>
    private static async Task<IResult> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        HttpContext context,
        TokenGrantHandler loginHandler,
        IExternalTokenVerifier verifier,
        IOptions<OpenEmrOptions> openEmrOptions,
        IOptions<BrowserFlowOptions> browserFlowOptions,
        OidcFlowCookieProtector flowProtector,
        SessionCookieProtector sessionProtector,
        CancellationToken ct)
    {
        var flowCookie = context.Request.Cookies[OidcFlowCookieProtector.CookieName];
        context.Response.Cookies.Delete(OidcFlowCookieProtector.CookieName);

        var flowState = flowProtector.TryUnprotect(flowCookie);
        if (flowState is null)
        {
            // The real returnUrl cannot be recovered without the cookie, so this is the one failure that
            // falls back to the configured default rather than the flow's own returnUrl.
            return FailureRedirect(browserFlowOptions.Value.DefaultReturnUrl, BrowserFlowErrorCodes.InvalidRequest);
        }

        if (error is not null)
        {
            return FailureRedirect(flowState.ReturnUrl, BrowserFlowErrorCodes.AccessDenied);
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) ||
            !string.Equals(state, flowState.State, StringComparison.Ordinal))
        {
            return FailureRedirect(flowState.ReturnUrl, BrowserFlowErrorCodes.InvalidRequest);
        }

        var result = await loginHandler.HandleAsync(code, flowState.CodeVerifier, openEmrOptions.Value.RedirectUri, ct);
        if (result is not LoginSucceeded succeeded)
        {
            return FailureRedirect(flowState.ReturnUrl, BrowserFlowErrorCodes.LoginFailed);
        }

        if (succeeded.Token.IdToken is not { } idToken)
        {
            return FailureRedirect(flowState.ReturnUrl, BrowserFlowErrorCodes.LoginFailed);
        }

        VerifiedToken? verifiedIdToken;
        try
        {
            verifiedIdToken = await verifier.VerifyAsync(idToken, ct);
        }
        catch (TokenVerifierUnavailableException)
        {
            return FailureRedirect(flowState.ReturnUrl, BrowserFlowErrorCodes.LoginFailed);
        }

        if (verifiedIdToken is null ||
            !string.Equals(verifiedIdToken.Nonce, flowState.Nonce, StringComparison.Ordinal))
        {
            return FailureRedirect(flowState.ReturnUrl, BrowserFlowErrorCodes.LoginFailed);
        }

        context.Response.Cookies.Append(
            SessionCookieProtector.CookieName,
            sessionProtector.Protect(succeeded.Token),
            SessionCookieOptions(context));

        return Results.Redirect(flowState.ReturnUrl);
    }

    /// <summary>Appends the error code to the redirect target, which may already carry its own query string.</summary>
    private static IResult FailureRedirect(string returnUrl, string errorCode) =>
        Results.Redirect(QueryHelpers.AddQueryString(returnUrl, "error", errorCode));

    /// <summary>
    /// <c>Secure</c> only when the request itself is HTTPS, so the flow still works on the local stack, which
    /// runs over plain HTTP (ADR 0020) — a cookie marked <c>Secure</c> is never sent back over HTTP.
    /// </summary>
    private static CookieOptions FlowCookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        MaxAge = OidcFlowCookieProtector.Lifetime,
        Path = "/"
    };

    /// <summary>Same reasoning as <see cref="FlowCookieOptions"/>.</summary>
    private static CookieOptions SessionCookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        MaxAge = SessionCookieProtector.Lifetime,
        Path = "/"
    };
}
