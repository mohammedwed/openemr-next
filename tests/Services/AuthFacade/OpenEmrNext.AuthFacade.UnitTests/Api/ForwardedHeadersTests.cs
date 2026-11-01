using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests that AuthFacade trusts <c>X-Forwarded-Proto</c> only from the configured network (ADR 0039) — the
/// gateway's, in Compose — and ignores it from anywhere else, so a request reaching AuthFacade's own
/// directly-published port cannot claim to be HTTPS when it is not.
/// </summary>
public class ForwardedHeadersTests
{
    private static async Task StartHostAsync(string trustedNetworkCidr)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = "https://openemr.test",
            ["OpenEmr:Issuer"] = "https://openemr.test/oauth2/default",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret",
            ["OpenEmr:RedirectUri"] = "https://facade.test/callback",
            ["BrowserFlow:DefaultReturnUrl"] = "https://app.test/",
            ["BrowserFlow:ReturnUrlAllowedHosts:0"] = "app.test",
            ["ForwardedHeaders:TrustedNetworkCidr"] = trustedNetworkCidr
        });

        builder.AddAuthFacadeHttpDependencies();

        await using var app = builder.Build();
        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task A_forwarded_https_header_from_the_trusted_network_is_honored()
    {
        // TestApi's default config trusts 127.0.0.1/32, and every test client connects from there.
        await using var api = await TestApi.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/test/is-https");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await api.Client.SendAsync(request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("isHttps").GetBoolean());
    }

    [Fact]
    public async Task No_forwarded_header_means_an_ordinary_http_request()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/test/is-https");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("isHttps").GetBoolean());
    }

    [Fact]
    public async Task A_forwarded_https_header_outside_the_trusted_network_is_ignored()
    {
        // The test client still connects from 127.0.0.1, but the configured trusted network excludes it --
        // simulating a request that reached AuthFacade directly rather than through the gateway.
        await using var api = await TestApi.StartAsync(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:TrustedNetworkCidr"] = "10.0.0.0/8"
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/test/is-https");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await api.Client.SendAsync(request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("isHttps").GetBoolean());
    }

    [Fact]
    public async Task The_openiddict_correlation_cookie_is_secure_behind_the_trusted_proxy()
    {
        // Companion to OpenIddictClientEndpointTests.Sets_a_lax_correlation_cookie_without_secure_on_plain_http,
        // which proves the opposite case: no forwarded header, no Secure.
        await using var api = await TestApi.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/oidc/login");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await api.BrowserClient.SendAsync(request);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("OpenIddict.Client.State."));
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Host_refuses_to_start_with_an_invalid_cidr()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync("not-a-cidr"));
    }

    [Fact]
    public async Task Host_refuses_to_start_with_a_missing_cidr()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync(""));
    }
}
