using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the login rate limit on <c>GET /oidc/login</c> and <c>/callback</c> (ADR 0038). The default policy
/// allows 10 requests a minute per client address, and every test client shares one loopback address, so the
/// first 10 requests in a test pass and the 11th is refused. <see cref="TestApi.BrowserClient"/> is used
/// throughout: <c>/oidc/login</c> never calls out to OpenEMR (it only builds a redirect and sets a
/// correlation cookie), so no fake server is needed to drive the limiter.
/// </summary>
public class RateLimitingTests
{
    /// <summary>Starts the API host with the given rate limit settings. Used by startup checks.</summary>
    private static async Task StartHostAsync(int permitLimit, TimeSpan window)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = "https://openemr-internal.test",
            ["OpenEmr:Issuer"] = "https://openemr.test/oauth2/default",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret",
            ["OpenEmr:RedirectUri"] = "https://facade.test/callback",
            ["BrowserFlow:DefaultReturnUrl"] = "https://app.test/",
            ["BrowserFlow:ReturnUrlAllowedHosts:0"] = "app.test",
            ["LoginRateLimit:PermitLimit"] = permitLimit.ToString(),
            ["LoginRateLimit:Window"] = window.ToString()
        });

        builder.AddAuthFacadeHttpDependencies();

        await using var app = builder.Build();
        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task Requests_over_the_login_limit_get_429_with_retry_after()
    {
        await using var api = await TestApi.StartAsync();

        for (var i = 0; i < 10; i++)
        {
            using var attempt = await api.BrowserClient.GetAsync("/oidc/login");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, attempt.StatusCode);
        }

        using var refused = await api.BrowserClient.GetAsync("/oidc/login");

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotNull(refused.Headers.RetryAfter);
        using var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        Assert.Equal("too_many_requests", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Login_under_the_limit_still_redirects_to_openemr()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.BrowserClient.GetAsync("/oidc/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Login_and_callback_share_one_budget_per_address()
    {
        // /callback requires the same policy as /oidc/login (OpenIddictClientEndpoints), so one address's
        // budget is shared across both steps of the flow, not doubled by splitting it across two routes.
        await using var api = await TestApi.StartAsync();

        for (var i = 0; i < 9; i++)
        {
            using var login = await api.BrowserClient.GetAsync("/oidc/login");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, login.StatusCode);
        }

        // The 10th request, on the other route, still fits inside the shared budget.
        using var callback = await api.BrowserClient.GetAsync("/callback");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, callback.StatusCode);

        // The 11th request, back on the first route, is the one that exhausts it.
        using var refused = await api.BrowserClient.GetAsync("/oidc/login");
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task Me_is_not_rate_limited_by_the_login_policy()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        var port = TestApi.GetFreeTcpPort();
        await using var api = await TestApi.StartAsync(
            new Dictionary<string, string?>
            {
                ["OpenEmr:BaseUrl"] = server.BaseUrl,
                ["OpenEmr:Issuer"] = server.Issuer,
                ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
            },
            port);
        using var loginResponse = await api.BrowserClient.GetAsync("/oidc/login");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(loginResponse.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");

        for (var i = 0; i < 15; i++)
        {
            using var me = await api.BrowserClient.GetAsync("/api/v1/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
    }

    [Fact]
    public async Task Host_refuses_to_start_with_a_permit_limit_below_one()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync(permitLimit: 0, window: TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Host_refuses_to_start_with_a_window_under_one_second()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync(permitLimit: 10, window: TimeSpan.FromMilliseconds(500)));
    }
}
