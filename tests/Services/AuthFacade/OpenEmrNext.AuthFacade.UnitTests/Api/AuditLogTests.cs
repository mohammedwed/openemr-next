using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the audit trail (ADR 0042): login outcomes and BFF proxy access, scoped there rather than
/// <c>/me</c> — the BFF proxy is the one place a signed-in session actually touches OpenEMR's data, which
/// is what a HIPAA-style audit trail cares about, and it fires far less often than <c>/me</c> does.
/// </summary>
public class AuditLogTests
{
    private static async Task<TestApi> StartApiAsync(FakeOpenEmrServer server, CapturingLoggerProvider logger)
    {
        var port = TestApi.GetFreeTcpPort();
        return await TestApi.StartAsync(
            new Dictionary<string, string?>
            {
                ["OpenEmr:BaseUrl"] = server.BaseUrl,
                ["OpenEmr:Issuer"] = server.Issuer,
                ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
            },
            port, logger);
    }

    private static async Task<(string State, string Nonce)> LoginAsync(TestApi api)
    {
        using var response = await api.BrowserClient.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        return (query["state"]!, query["nonce"]!);
    }

    [Fact]
    public async Task A_successful_login_is_audited_with_the_subject()
    {
        var logger = new CapturingLoggerProvider();
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, logger);
        server.Subject = "subject-audit-1";
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;

        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        Assert.Contains(logger.Entries, e => e.Contains("Login succeeded for subject subject-audit-1"));
    }

    [Fact]
    public async Task A_failed_login_is_audited_with_a_fixed_reason_not_openemrs_own_text()
    {
        var logger = new CapturingLoggerProvider();
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, logger);
        var (state, _) = await LoginAsync(api);
        server.Nonce = "a-different-nonce"; // forces the nonce-mismatch failure

        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        Assert.Contains(logger.Entries, e => e.Contains("Login failed: rejected"));
    }

    [Fact]
    public async Task Bff_proxy_access_is_audited_with_subject_method_path_and_status()
    {
        var logger = new CapturingLoggerProvider();
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, logger);
        server.Subject = "subject-audit-2";
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        using var response = await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(logger.Entries, e =>
            e.Contains("subject-audit-2") && e.Contains("GET") &&
            e.Contains("/bff/openemr/apis/default/api/ping") && e.Contains("200"));
    }

    [Fact]
    public async Task The_access_token_never_appears_in_the_audit_trail()
    {
        // Companion to RequestLoggingTests' own "never in any log" guard, scoped to the one credential the
        // BFF proxy's audit entry sits right next to attaching (ADR 0042).
        var logger = new CapturingLoggerProvider();
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, logger);
        var (state, nonce) = await LoginAsync(api);
        server.Nonce = nonce;
        await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={state}");

        await api.BrowserClient.GetAsync("/bff/openemr/apis/default/api/ping");

        Assert.DoesNotContain(logger.Entries, e => e.Contains("fake-access-token"));
    }
}
