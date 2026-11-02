using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the BFF proxy's silent refresh of an expired access token (ADR 0045) — the gap ADR 0029 named and
/// deferred: "no silent refresh happens when the session's access token expires." Only the BFF proxy needs
/// this; <c>/me</c> never calls OpenEMR.
/// </summary>
public class AccessTokenRefreshTests
{
    private static async Task<(TestApi Api, FakeOpenEmrServer Server)> StartLoggedInApiAsync(int accessTokenExpiresInSeconds)
    {
        var server = await FakeOpenEmrServer.StartAsync();
        server.AccessTokenExpiresInSeconds = accessTokenExpiresInSeconds;
        var port = TestApi.GetFreeTcpPort();
        var api = await TestApi.StartAsync(
            new Dictionary<string, string?>
            {
                ["OpenEmr:BaseUrl"] = server.BaseUrl,
                ["OpenEmr:Issuer"] = server.Issuer,
                ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
            },
            port);

        using var login = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        using var callback = await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);

        return (api, server);
    }

    private static async Task<string> PingAuthorizationHeaderAsync(TestApi api)
    {
        using var response = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/ping");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("authorization").GetString()!;
    }

    [Fact]
    public async Task A_near_expiry_access_token_is_silently_refreshed_before_the_proxied_call()
    {
        // 10 seconds, not already negative: OpenIddict.Client itself rejects a token response whose own
        // expires_in is negative during the original authorization-code exchange, so this test instead
        // relies on AccessTokenRefresher's own skew window (30s) treating anything this close as expired.
        var (api, server) = await StartLoggedInApiAsync(accessTokenExpiresInSeconds: 10);
        await using var apiDisposable = api;
        await using var serverDisposable = server;

        var authorization = await PingAuthorizationHeaderAsync(api);

        Assert.Equal("Bearer refreshed-access-token", authorization);
        Assert.Equal(1, server.RefreshEndpointCallCount);
    }

    [Fact]
    public async Task A_refreshed_token_is_persisted_so_a_second_call_does_not_refresh_again()
    {
        var (api, server) = await StartLoggedInApiAsync(accessTokenExpiresInSeconds: 10);
        await using var apiDisposable = api;
        await using var serverDisposable = server;

        await PingAuthorizationHeaderAsync(api);
        var secondAuthorization = await PingAuthorizationHeaderAsync(api);

        Assert.Equal("Bearer refreshed-access-token", secondAuthorization);
        Assert.Equal(1, server.RefreshEndpointCallCount);
    }

    [Fact]
    public async Task A_still_valid_access_token_is_not_refreshed()
    {
        var (api, server) = await StartLoggedInApiAsync(accessTokenExpiresInSeconds: 3600);
        await using var apiDisposable = api;
        await using var serverDisposable = server;

        var authorization = await PingAuthorizationHeaderAsync(api);

        Assert.Equal("Bearer fake-access-token", authorization);
        Assert.Equal(0, server.RefreshEndpointCallCount);
    }

    [Fact]
    public async Task A_rejected_refresh_falls_back_to_the_stale_token_instead_of_failing_the_request()
    {
        var (api, server) = await StartLoggedInApiAsync(accessTokenExpiresInSeconds: 10);
        await using var apiDisposable = api;
        await using var serverDisposable = server;
        server.RefreshEndpointStatusCode = 400;

        var authorization = await PingAuthorizationHeaderAsync(api);

        Assert.Equal("Bearer fake-access-token", authorization);
        Assert.Equal(1, server.RefreshEndpointCallCount);
    }
}
