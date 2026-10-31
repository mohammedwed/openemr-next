using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// The reverse proxy that lets a logged-in browser session reach OpenEMR's own API without ever holding a
/// bearer token itself — the token-handling proxy ADR 0027 deferred. <see cref="FakeOpenEmrServer"/> stands
/// in for OpenEMR's protected API: <c>/apis/default/api/ping</c> reports back the request header it actually
/// received, and <c>/apis/default/api/missing-patient</c> always answers 404, for the relay tests.
/// </summary>
public class OpenEmrBffProxyEndpointTests
{
    /// <summary>Same reasoning as <see cref="OpenIddictClientCallbackTests"/>'s own helper.</summary>
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

    private static async Task LogInAsync(TestApi api, FakeOpenEmrServer server)
    {
        using var login = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");
    }

    [Fact]
    public async Task A_logged_in_session_reaches_openemrs_api_with_the_real_token_attached()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        await LogInAsync(api, server);

        using var response = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/ping");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer fake-access-token", body.RootElement.GetProperty("authorization").GetString());
    }

    [Fact]
    public async Task A_request_with_no_session_cookie_is_rejected_before_reaching_openemr()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);

        using var response = await api.Client.GetAsync("/bff/openemr/apis/default/api/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_bearer_token_alone_does_not_satisfy_the_proxys_session_requirement()
    {
        // Regression guard: the proxy requires the AuthFacadeSession scheme specifically (unlike /me, which
        // accepts either). A caller that already holds a bearer token has no need for this proxy and should
        // call OpenEMR directly, so the default dual-scheme policy must not apply here.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/openemr/apis/default/api/ping");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "some-bearer-token");

        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_session_with_no_token_forwards_with_no_authorization_header()
    {
        // The proxy makes no auth decision of its own (ADR 0013's passthrough): a session that is
        // authenticated but holds no access token -- a ticket that predates ADR 0027, or one OpenIddict
        // never captured a token for -- still reaches OpenEMR, just with nothing to attach.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        await api.BrowserClient.GetAsync("/test/sign-in-without-tokens");

        using var response = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/ping");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("", body.RootElement.GetProperty("authorization").GetString());
    }

    [Fact]
    public async Task A_non_200_response_from_openemr_relays_back_unchanged()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        await LogInAsync(api, server);

        using var response = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/missing-patient");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", body.RootElement.GetProperty("error").GetString());
    }
}
