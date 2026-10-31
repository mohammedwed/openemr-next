using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Full round-trip tests for <c>/callback</c> against a real fake OpenEMR server
/// (<see cref="FakeOpenEmrServer"/>), covering the one part of the OpenIddict-driven flow (ADR 0026) that
/// <c>/oidc/login</c>'s own tests cannot reach: the actual HTTP calls to the token and JWKS endpoints, and
/// <c>id_token</c> signature/issuer/audience/nonce validation against a token OpenIddict.Client really parsed.
/// </summary>
public class OpenIddictClientCallbackTests
{
    /// <summary>
    /// Starts <see cref="TestApi"/> on a port known ahead of time, with <c>OpenEmr:RedirectUri</c> matching
    /// it: OpenIddict.Client recognizes <c>/callback</c> as its own redirection endpoint by matching the
    /// incoming request's actual host against that setting (<see cref="TestApi.StartAsync"/>'s own remarks),
    /// so the default placeholder <c>https://facade.test/callback</c> would never match a loopback test
    /// client's real request and every callback would silently fall through as an unrecognized endpoint.
    /// </summary>
    private static async Task<TestApi> StartApiAsync(FakeOpenEmrServer server)
    {
        var port = TestApi.GetFreeTcpPort();
        return await TestApi.StartAsync(
            new Dictionary<string, string?>
            {
                ["OpenEmr:BaseUrl"] = server.BaseUrl,
                ["OpenEmr:Issuer"] = server.Issuer,
                ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
            },
            port);
    }

    /// <summary>Calls <c>/oidc/login</c> and returns its <c>state</c> and <c>nonce</c>, to feed back to <c>/callback</c>.</summary>
    private static async Task<(string State, string Nonce)> LoginAsync(TestApi api)
    {
        using var response = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        return (query["state"]!, query["nonce"]!);
    }

    [Fact]
    public async Task Successful_callback_signs_in_and_redirects_to_the_default_return_url()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("https://app.test/", callback.Headers.Location!.ToString());
        // ASP.NET Core prefixes the cookie scheme's name by default (".AspNetCore.AuthFacadeSession"); the
        // scheme name itself (AuthFacadeSession) is not the literal cookie name.
        var setCookies = callback.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookies, c => c.StartsWith(".AspNetCore.AuthFacadeSession="));
    }

    [Fact]
    public async Task Session_cookie_from_a_successful_login_authorizes_a_later_api_call()
    {
        // Regression guard: /api/v1/me's RequireAuthorization() used to resolve to a policy that only ever
        // authenticated the Bearer scheme, so the AuthFacadeSession cookie /callback just signed in with
        // satisfied nothing -- a browser that completed the OIDC login still could not call the API.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        server.Subject = "subject-42";
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        using var me = await api.BrowserClient.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("subject-42", await me.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Successful_callback_persists_the_real_openemr_access_and_refresh_token()
    {
        // Without this, the real OpenEMR token OpenIddict.Client already holds during the exchange is
        // discarded the moment /callback returns, and a browser client that was only ever given the
        // session cookie (never a bearer token, by design) has no way to reach OpenEMR's API later.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        using var response = await api.BrowserClient.GetAsync("/test/session-tokens");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("fake-access-token", body.RootElement.GetProperty("accessToken").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("expiresAt").GetString()));
    }

    [Fact]
    public async Task Callback_with_a_mismatched_nonce_redirects_with_login_failed()
    {
        // Regression guard: an id_token whose nonce does not match the one /oidc/login stashed must not be
        // accepted, even though its signature, issuer and audience are all genuinely valid.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, _) = await LoginAsync(api);
        server.Nonce = "a-different-nonce";

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        Assert.Equal("https://app.test/?error=login_failed", callback.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Callback_whose_grant_openemr_rejects_redirects_with_login_failed()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        server.TokenEndpointStatusCode = 400;

        using var callback = await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        Assert.Equal("https://app.test/?error=login_failed", callback.Headers.Location!.ToString());
    }
}
