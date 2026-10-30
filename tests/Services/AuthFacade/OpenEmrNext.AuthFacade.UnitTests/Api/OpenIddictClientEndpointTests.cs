using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests <c>GET /oidc/login</c>, the OpenIddict-driven browser authorization-code flow (ADR 0026), in-process
/// through <see cref="TestApi"/>. The challenge step builds a URL and sets a cookie without making any
/// outbound call, so no fake OpenEMR server is needed here; <c>/callback</c>'s full round trip — which does
/// call out — is covered separately against a fake OpenEMR server.
/// </summary>
public class OpenIddictClientEndpointTests
{
    [Fact]
    public async Task Redirects_to_the_issuer_not_the_base_url()
    {
        // Regression guard: /oidc/login once built this from OpenEmr:BaseUrl (the container-internal
        // address), which the browser cannot reach — confirmed only by a live login attempt (ADR 0026).
        // TestApi configures BaseUrl and Issuer to different hosts specifically so this test would fail if
        // that regressed.
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync("/oidc/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://openemr.test/oauth2/default/authorize", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Authorize_request_carries_the_client_id_redirect_uri_and_scope()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync("/oidc/login");

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("facade-client", query["client_id"]);
        Assert.Equal("https://facade.test/callback", query["redirect_uri"]);
        Assert.Equal("openid profile email offline_access", query["scope"]);
    }

    [Fact]
    public async Task Authorize_request_carries_pkce_state_and_nonce()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync("/oidc/login");

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.False(string.IsNullOrEmpty(query["nonce"]));
    }

    [Fact]
    public async Task Sets_a_lax_correlation_cookie_without_secure_on_plain_http()
    {
        // Regression guard: the correlation cookie once defaulted to SameSite=None with Secure forced on,
        // which a real browser never sent back over this stack's plain HTTP (ID2129, ADR 0026) — confirmed
        // only by a live login attempt, since every in-process tool used during debugging (including an
        // earlier version of this same check, via curl) still sent it regardless.
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync("/oidc/login");

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("OpenIddict.Client.State."));
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Returns_a_different_state_and_nonce_on_each_call()
    {
        await using var api = await TestApi.StartAsync();

        using var first = await api.BrowserClient.GetAsync("/oidc/login");
        using var second = await api.BrowserClient.GetAsync("/oidc/login");

        var firstQuery = QueryHelpers.ParseQuery(first.Headers.Location!.Query);
        var secondQuery = QueryHelpers.ParseQuery(second.Headers.Location!.Query);
        Assert.NotEqual(firstQuery["state"], secondQuery["state"]);
        Assert.NotEqual(firstQuery["nonce"], secondQuery["nonce"]);
    }
}
