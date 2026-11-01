using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Confirms that no request body ever reaches a log, across every logger category down to
/// <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/>. AuthFacade itself calls no logger directly
/// (confirmed by inspection); this guards against a regression from the framework, a library, or code
/// added later — the BFF proxy is the one place a body still flows through today, and it is exactly
/// where real patient data will flow once a domain service exists behind it.
/// </summary>
public class RequestLoggingTests
{
    /// <summary>Same reasoning as <see cref="OpenIddictClientCallbackTests"/>'s own helper.</summary>
    private static async Task<TestApi> StartApiAsync(FakeOpenEmrServer server, CapturingLoggerProvider logs)
    {
        var port = TestApi.GetFreeTcpPort();
        return await TestApi.StartAsync(
            new Dictionary<string, string?>
            {
                ["OpenEmr:BaseUrl"] = server.BaseUrl,
                ["OpenEmr:Issuer"] = server.Issuer,
                ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
            },
            port,
            logs);
    }

    private static async Task LogInAsync(TestApi api, FakeOpenEmrServer server)
    {
        using var login = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");
    }

    [Fact]
    public async Task A_request_body_proxied_to_openemr_never_appears_in_any_log()
    {
        const string Marker = "request-body-marker-f3b9c1a2-ssn-123-45-6789";
        var logs = new CapturingLoggerProvider();
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, logs);
        await LogInAsync(api, server);

        using var body = new StringContent($"{{\"note\":\"{Marker}\"}}", System.Text.Encoding.UTF8, "application/json");
        using var response = await api.BrowserClient.PostAsync("/bff/openemr/apis/default/api/echo", body);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        // Guards against this test passing vacuously if the capture wiring itself ever breaks: there must
        // be real log output to search, not an empty collection that trivially contains no marker.
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(Marker, StringComparison.Ordinal));
    }
}
