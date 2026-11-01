using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the circuit breakers that guard AuthFacade's two remaining calls to OpenEMR (ADR 0041): the token
/// exchange during <c>/callback</c>, and the BFF proxy's forwarded API calls. The JWKS fetch's own breaker
/// is tested directly in <see cref="OpenEmrConfigurationManagerTests"/>, since it needs no live host.
/// Thresholds are overridden low in every test here so a breaker opens within a handful of calls instead of
/// the production defaults' thirty-second window.
/// </summary>
public class CircuitBreakerTests
{
    private static readonly Dictionary<string, string?> LowThresholds = new()
    {
        ["OpenEmr:CircuitBreaker:MinimumThroughput"] = "2",
        ["OpenEmr:CircuitBreaker:SamplingDuration"] = "00:00:30",
        ["OpenEmr:CircuitBreaker:BreakDuration"] = "00:00:30"
    };

    private static async Task<TestApi> StartApiAsync(FakeOpenEmrServer server, Dictionary<string, string?>? extra = null)
    {
        var port = TestApi.GetFreeTcpPort();
        var overrides = new Dictionary<string, string?>(LowThresholds)
        {
            ["OpenEmr:BaseUrl"] = server.BaseUrl,
            ["OpenEmr:Issuer"] = server.Issuer,
            ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
        };
        if (extra is not null)
        {
            foreach (var (key, value) in extra) overrides[key] = value;
        }

        return await TestApi.StartAsync(overrides, port);
    }

    /// <summary>Drives a fresh <c>/oidc/login</c> + <c>/callback</c> pair, so each attempt gets its own usable state token.</summary>
    private static async Task<HttpResponseMessage> AttemptCallbackAsync(TestApi api, FakeOpenEmrServer server)
    {
        using var login = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        return await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");
    }

    [Fact]
    public async Task Repeated_token_endpoint_failures_open_the_circuit_and_stop_calling_openemr()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        server.TokenEndpointStatusCode = 500;

        // MinimumThroughput=2: these two failed attempts open the circuit.
        using var first = await AttemptCallbackAsync(api, server);
        using var second = await AttemptCallbackAsync(api, server);
        var callsOnceOpen = server.TokenEndpointCallCount;

        using var third = await AttemptCallbackAsync(api, server);

        // The breaker refused the third attempt before it ever reached the token endpoint.
        Assert.Equal(callsOnceOpen, server.TokenEndpointCallCount);
        // All three still degrade to the same generic failure redirect, not a raw 500 to the browser.
        foreach (var response in new[] { first, second, third })
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("https://app.test/?error=login_failed", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task A_rejected_grant_does_not_count_toward_opening_the_circuit()
    {
        // A 4xx (a bad or replayed code) is a normal rejection, not an OpenEMR outage -- it must never
        // contribute to opening the circuit, however many times it happens.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        server.TokenEndpointStatusCode = 400;

        for (var i = 0; i < 5; i++)
        {
            await AttemptCallbackAsync(api, server);
        }

        Assert.Equal(5, server.TokenEndpointCallCount);
    }

    [Fact]
    public async Task Repeated_bff_proxy_failures_open_the_circuit_and_answer_503_without_calling_openemr()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        using var login = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");
        server.FlakyEndpointStatusCode = 500;

        using var first = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/flaky");
        using var second = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/flaky");
        var callsOnceOpen = server.FlakyEndpointCallCount;

        using var third = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/flaky");

        Assert.Equal(callsOnceOpen, server.FlakyEndpointCallCount);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, third.StatusCode);
    }

    [Fact]
    public async Task A_404_from_openemr_does_not_count_toward_opening_the_bff_proxys_circuit()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        using var login = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");

        for (var i = 0; i < 5; i++)
        {
            using var response = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/missing-patient");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
