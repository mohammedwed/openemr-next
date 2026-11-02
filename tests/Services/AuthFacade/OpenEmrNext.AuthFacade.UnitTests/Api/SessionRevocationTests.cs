using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests that AuthFacade's own session state (ADR 0044) is actually revocable: the
/// <c>AuthFacadeSession</c> cookie carries only a key into a server-side record now, not the whole ticket,
/// so deleting that record -- not just deleting the cookie client-side -- is what ends a session for good,
/// even for a copy of the cookie this process never saw again.
/// </summary>
public class SessionRevocationTests
{
    private static async Task<TestApi> StartApiAsync(FakeOpenEmrServer server, string? adminSubject = null)
    {
        var port = TestApi.GetFreeTcpPort();
        var overrides = new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = server.BaseUrl,
            ["OpenEmr:Issuer"] = server.Issuer,
            ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback"
        };
        if (adminSubject is not null)
        {
            overrides["Admin:Subjects:0"] = adminSubject;
        }

        return await TestApi.StartAsync(overrides, port);
    }

    /// <summary>
    /// Drives a fresh login as its own independent "browser": a client with its own cookie jar, so two
    /// calls to this against the same <see cref="TestApi"/> produce two genuinely separate sessions, the
    /// way two different devices signing in as the same OpenEMR user would.
    /// </summary>
    private static async Task<HttpClient> LoginAsNewBrowserAsync(TestApi api, FakeOpenEmrServer server)
    {
        var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = api.Client.BaseAddress
        };
        using var login = await browser.GetAsync("/oidc/login");
        var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
        server.Nonce = query["nonce"]!;
        using var callback = await browser.GetAsync($"/callback?code=fake-code&state={query["state"]}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        return browser;
    }

    [Fact]
    public async Task Revoking_a_session_signs_it_out_immediately()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        using var browser = await LoginAsNewBrowserAsync(api, server);

        using var before = await browser.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var revoke = await browser.PostAsync("/sessions/revoke-all", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var after = await browser.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Revoking_all_sessions_ends_every_session_for_the_same_subject_not_just_the_caller()
    {
        // The actual point of this feature: a session an attacker holds is a *different* session record
        // from the one the legitimate user is calling revoke-all from, so ending "the caller's session"
        // alone would do nothing about it. Both must die.
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        server.Subject = "shared-subject";
        using var browserA = await LoginAsNewBrowserAsync(api, server);
        using var browserB = await LoginAsNewBrowserAsync(api, server);

        using var revoke = await browserA.PostAsync("/sessions/revoke-all", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var meA = await browserA.GetAsync("/api/v1/me");
        using var meB = await browserB.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meA.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meB.StatusCode);
    }

    [Fact]
    public async Task Revoking_all_sessions_does_not_affect_a_different_subjects_session()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);
        server.Subject = "subject-a";
        using var browserA = await LoginAsNewBrowserAsync(api, server);
        server.Subject = "subject-b";
        using var browserB = await LoginAsNewBrowserAsync(api, server);

        using var revoke = await browserA.PostAsync("/sessions/revoke-all", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var meB = await browserB.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, meB.StatusCode);
    }

    [Fact]
    public async Task Revoke_all_requires_an_authenticated_session()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server);

        using var response = await api.Client.PostAsync("/sessions/revoke-all", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_allowlisted_admin_subject_can_revoke_another_subjects_sessions()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, adminSubject: "admin-subject");
        server.Subject = "admin-subject";
        using var admin = await LoginAsNewBrowserAsync(api, server);
        server.Subject = "target-subject";
        using var target = await LoginAsNewBrowserAsync(api, server);

        using var revoke = await admin.PostAsync("/sessions/revoke/target-subject", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var meTarget = await target.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meTarget.StatusCode);
        // The admin only revoked someone else's sessions; their own is untouched.
        using var meAdmin = await admin.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, meAdmin.StatusCode);
    }

    [Fact]
    public async Task An_admin_revoking_their_own_subject_is_signed_out_too()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, adminSubject: "admin-subject");
        server.Subject = "admin-subject";
        using var admin = await LoginAsNewBrowserAsync(api, server);

        using var revoke = await admin.PostAsync("/sessions/revoke/admin-subject", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var me = await admin.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task A_subject_not_on_the_admin_allowlist_gets_forbidden_and_changes_nothing()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        // No Admin:Subjects configured at all -- the default, fail-closed allowlist.
        await using var api = await StartApiAsync(server);
        server.Subject = "not-an-admin";
        using var caller = await LoginAsNewBrowserAsync(api, server);
        server.Subject = "target-subject";
        using var target = await LoginAsNewBrowserAsync(api, server);

        using var revoke = await caller.PostAsync("/sessions/revoke/target-subject", null);
        Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);

        using var meTarget = await target.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, meTarget.StatusCode);
    }

    [Fact]
    public async Task Admin_revoke_requires_an_authenticated_session()
    {
        await using var server = await FakeOpenEmrServer.StartAsync();
        await using var api = await StartApiAsync(server, adminSubject: "admin-subject");

        using var response = await api.Client.PostAsync("/sessions/revoke/some-subject", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
