using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tested <c>GET /login</c> and <c>GET /callback</c>, the hand-rolled browser authorization-code flow
/// (ADR 0024). Superseded by the OpenIddict-driven flow (<c>OpenIddictClientEndpoints</c>), which took over
/// the same <c>/callback</c> redirect_uri, so <c>BrowserFlowEndpoints</c> is no longer mapped and these tests
/// can no longer reach it. Left in place, skipped, rather than deleted, in case the hand-rolled flow is
/// revisited; delete this file once that's no longer a possibility.
/// </summary>
public class BrowserFlowEndpointTests
{
    private const string SkipReason =
        "BrowserFlowEndpoints is no longer mapped; the OpenIddict-driven flow replaced it at the same /callback path.";

    private const string AllowedReturnUrl = "https://app.test/dashboard";
    private const string DefaultReturnUrl = "https://app.test/";

    /// <summary>The token OpenEMR issues for an accepted code exchange, including an <c>id_token</c>.</summary>
    private static OpenEmrToken SuccessToken(string idToken) => new("openemr-token", "Bearer", 3600, "r-1", idToken);

    [Fact(Skip = SkipReason)]
    public async Task Login_redirects_to_openemr_authorize_with_pkce_state_and_nonce()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync($"/login?returnUrl={Uri.EscapeDataString(AllowedReturnUrl)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith("https://openemr.test/oauth2/default/authorize", location.ToString());

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("facade-client", query["client_id"]);
        Assert.Equal("https://facade.test/callback", query["redirect_uri"]);
        Assert.Equal("openid profile email offline_access", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.False(string.IsNullOrEmpty(query["nonce"]));
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("authfacade_oidc=", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        // The test host runs over plain HTTP; a Secure cookie would never come back over it (ADR 0024).
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = SkipReason)]
    public async Task Login_with_a_disallowed_return_url_falls_back_to_the_default()
    {
        await using var api = await TestApi.StartAsync();
        var (state, nonce) = await LoginAsync(api, "https://evil.test/steal");

        api.IdentityProvider.Result = new PasswordGrantResult(200, SuccessToken("id-1"));
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null, nonce);

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=auth-code&state={state}");

        Assert.Equal(DefaultReturnUrl, callback.Headers.Location!.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task Callback_without_a_flow_cookie_redirects_to_the_default_with_invalid_request()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync("/callback?code=auth-code&state=whatever");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"{DefaultReturnUrl}?error=invalid_request", response.Headers.Location!.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task Callback_with_a_mismatched_state_redirects_with_invalid_request_and_clears_the_cookie()
    {
        await using var api = await TestApi.StartAsync();
        await LoginAsync(api, AllowedReturnUrl);

        using var callback = await api.BrowserClient.GetAsync("/callback?code=auth-code&state=not-the-real-state");

        Assert.Equal($"{AllowedReturnUrl}?error=invalid_request", callback.Headers.Location!.ToString());
        var setCookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("authfacade_oidc=;", setCookie);
    }

    [Fact(Skip = SkipReason)]
    public async Task Callback_with_openemr_error_redirects_with_access_denied()
    {
        await using var api = await TestApi.StartAsync();
        await LoginAsync(api, AllowedReturnUrl);

        using var callback = await api.BrowserClient.GetAsync("/callback?error=access_denied");

        Assert.Equal($"{AllowedReturnUrl}?error=access_denied", callback.Headers.Location!.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task Callback_whose_grant_openemr_rejects_redirects_with_login_failed()
    {
        await using var api = await TestApi.StartAsync();
        var (state, _) = await LoginAsync(api, AllowedReturnUrl);
        api.IdentityProvider.Result = new PasswordGrantResult(400);

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=auth-code&state={state}");

        Assert.Equal($"{AllowedReturnUrl}?error=login_failed", callback.Headers.Location!.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task Callback_whose_token_has_no_id_token_redirects_with_login_failed()
    {
        await using var api = await TestApi.StartAsync();
        var (state, _) = await LoginAsync(api, AllowedReturnUrl);
        api.IdentityProvider.Result = new PasswordGrantResult(200, new OpenEmrToken("openemr-token", "Bearer", 3600, "r-1"));

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=auth-code&state={state}");

        Assert.Equal($"{AllowedReturnUrl}?error=login_failed", callback.Headers.Location!.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task Callback_whose_id_token_nonce_does_not_match_redirects_with_login_failed()
    {
        await using var api = await TestApi.StartAsync();
        var (state, _) = await LoginAsync(api, AllowedReturnUrl);
        api.IdentityProvider.Result = new PasswordGrantResult(200, SuccessToken("id-1"));
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null, "a-different-nonce");

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=auth-code&state={state}");

        Assert.Equal($"{AllowedReturnUrl}?error=login_failed", callback.Headers.Location!.ToString());
    }

    [Fact(Skip = SkipReason)]
    public async Task Successful_callback_sets_the_session_cookie_and_redirects_to_the_return_url()
    {
        await using var api = await TestApi.StartAsync();
        var (state, nonce) = await LoginAsync(api, AllowedReturnUrl);
        api.IdentityProvider.Result = new PasswordGrantResult(200, SuccessToken("id-1"));
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null, nonce);

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=auth-code&state={state}");

        Assert.Equal(AllowedReturnUrl, callback.Headers.Location!.ToString());
        var setCookies = callback.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookies, c => c.StartsWith("authfacade_session=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        // The flow cookie is always cleared, win or lose.
        Assert.Contains(setCookies, c => c.StartsWith("authfacade_oidc=;"));
    }

    [Fact(Skip = SkipReason)]
    public async Task Successful_callback_sends_the_code_and_verifier_from_the_flow_cookie_to_the_identity_provider()
    {
        await using var api = await TestApi.StartAsync();
        var (state, nonce) = await LoginAsync(api, AllowedReturnUrl);
        api.IdentityProvider.Result = new PasswordGrantResult(200, SuccessToken("id-1"));
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null, nonce);

        await api.BrowserClient.GetAsync($"/callback?code=auth-code&state={state}");

        Assert.Equal("auth-code", api.IdentityProvider.ReceivedCode);
        Assert.Equal("https://facade.test/callback", api.IdentityProvider.ReceivedRedirectUri);
        Assert.False(string.IsNullOrEmpty(api.IdentityProvider.ReceivedCodeVerifier));
    }

    /// <summary>Calls <c>/login</c> on <paramref name="api"/>'s browser client and returns its <c>state</c> and <c>nonce</c>.</summary>
    private static async Task<(string State, string Nonce)> LoginAsync(TestApi api, string returnUrl)
    {
        using var response = await api.BrowserClient.GetAsync($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        return (query["state"]!, query["nonce"]!);
    }
}
