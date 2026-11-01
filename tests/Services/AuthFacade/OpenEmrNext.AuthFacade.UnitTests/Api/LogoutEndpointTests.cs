using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests <c>GET /logout</c> (ADR 0040): RP-initiated logout against OpenEMR's own <c>end_session_endpoint</c>,
/// which requires the original <c>id_token</c> as <c>id_token_hint</c> — confirmed against the real server,
/// where a bare request without one is refused with <c>invalid_request</c>.
/// </summary>
public class LogoutEndpointTests
{
    /// <summary>Same port-matching setup <c>OpenIddictClientCallbackTests</c> uses, so <c>/callback</c> is recognized.</summary>
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
    public async Task Logout_redirects_to_openemrs_end_session_endpoint_with_the_real_id_token()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        using var logout = await api.BrowserClient.GetAsync("/logout");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        var location = logout.Headers.Location!;
        Assert.StartsWith($"{server.Issuer}/logout", location.ToString());
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.False(string.IsNullOrEmpty(query["id_token_hint"]));
        // Fixed, not caller-supplied: OpenEMR only redirects back to a post_logout_redirect_uri that
        // matches its own registration byte for byte (ADR 0040), unlike /oidc/login's returnUrl.
        Assert.Equal("https://app.test/", query["post_logout_redirect_uri"]);
    }

    [Fact]
    public async Task Logout_clears_the_session_cookie_so_a_later_api_call_is_unauthenticated()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");
        await api.BrowserClient.GetAsync("/logout");

        using var me = await api.BrowserClient.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_session_redirects_straight_to_the_default_return_url_without_calling_openemr()
    {
        // No login happened, so there is no id_token to send -- and nothing for OpenEMR to end. Asserting
        // this redirects at all (rather than erroring) is itself the point: OpenEmr:BaseUrl/Issuer here are
        // TestApi's default unreachable placeholder hosts, so a call to OpenEMR would hang or fail.
        await using var api = await TestApi.StartAsync();

        using var logout = await api.BrowserClient.GetAsync("/logout");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("https://app.test/", logout.Headers.Location!.ToString());
    }
}
