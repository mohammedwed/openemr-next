using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Runs the real AuthFacade pipeline on a random local port. Needs no Docker stack and no extra test packages.
/// </summary>
internal sealed class TestApi : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApi(WebApplication app)
    {
        _app = app;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        // The browser flow (ADR 0024) is tested across several requests that must carry the cookies the
        // previous response set, without the client following the redirects itself.
        BrowserClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(app.Urls.First())
        };
    }

    /// <summary>An HTTP client whose base address is the running API.</summary>
    public HttpClient Client { get; }

    /// <summary>
    /// A client that does not follow redirects and keeps cookies across requests, for the browser flow
    /// (ADR 0024): tests read the <c>Location</c> and <c>Set-Cookie</c> headers directly.
    /// </summary>
    public HttpClient BrowserClient { get; }

    /// <summary>
    /// Starts the API on a random loopback port with in-memory settings and a fake OpenEMR
    /// identity provider. The fake is registered last, so it replaces the real one.
    /// </summary>
    /// <param name="configOverrides">
    /// Settings applied after the defaults, so they win — for a test that needs <c>OpenEmr:BaseUrl</c> and
    /// <c>OpenEmr:Issuer</c> to point at a real fake OpenEMR server (<see cref="FakeOpenEmrServer"/>) instead
    /// of the default unreachable placeholder hosts, to exercise a real HTTP round trip.
    /// </param>
    /// <param name="port">
    /// A specific loopback port to bind, instead of letting the OS pick one. OpenIddict.Client recognizes
    /// <c>/callback</c> as its own redirection endpoint by matching the incoming request's actual host
    /// against the registered <c>OpenEmr:RedirectUri</c> (the same way Compose's port mapping preserves the
    /// real <c>Host</c> header through to the container) — a test that drives <c>/callback</c> for real, not
    /// just <c>/oidc/login</c>, needs its own port known ahead of time so it can set a matching
    /// <c>OpenEmr:RedirectUri</c> override. Use <see cref="GetFreeTcpPort"/> to pick one.
    /// </param>
    /// <param name="loggerProvider">
    /// Attached to the host's logging pipeline with every category opened down to <see cref="LogLevel.Trace"/>,
    /// for a test that asserts something never appears in a log, across any category — not given by default,
    /// since capturing every log line is otherwise pure overhead.
    /// </param>
    /// <returns>A running instance. Dispose it to stop the host.</returns>
    public static async Task<TestApi> StartAsync(
        Dictionary<string, string?>? configOverrides = null, int? port = null, ILoggerProvider? loggerProvider = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseUrls($"http://127.0.0.1:{port ?? 0}");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Deliberately different hosts, the same way Compose's container-internal BaseUrl diverges
            // from the publicly-reachable Issuer: a browser-flow test that used the wrong one would fail.
            ["OpenEmr:BaseUrl"] = "https://openemr-internal.test",
            ["OpenEmr:Issuer"] = "https://openemr.test/oauth2/default",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret",
            ["OpenEmr:RedirectUri"] = "https://facade.test/callback",
            ["BrowserFlow:DefaultReturnUrl"] = "https://app.test/",
            ["BrowserFlow:ReturnUrlAllowedHosts:0"] = "app.test",
            // Every test client connects from loopback (see WebHost.UseUrls below), so this is the one
            // network a test needs trusted to simulate a request that arrived through the gateway (ADR 0039).
            ["ForwardedHeaders:TrustedNetworkCidr"] = "127.0.0.1/32"
        });
        if (configOverrides is not null)
        {
            builder.Configuration.AddInMemoryCollection(configOverrides);
        }
        if (loggerProvider is not null)
        {
            builder.Logging.SetMinimumLevel(LogLevel.Trace).AddProvider(loggerProvider);
        }

        builder.AddAuthFacadeHttpDependencies();

        var app = builder.Build();
        await app.EnsureAuthFacadeDatabaseCreatedAsync();
        app.UseAuthFacadeHttpMiddleware();
        app.MapAuthFacadeEndpoints();
        app.MapOpenApi();
        // Test-only: reads back the tokens the session cookie carries, the same way a future
        // token-handling proxy endpoint would with HttpContext.GetTokenAsync(SessionScheme, ...) -- not
        // mapped in production, and the only way to prove the round trip through the real, encrypted
        // cookie ticket without reimplementing ASP.NET Core's own cookie protection in the test.
        app.MapGet("/test/session-tokens", async (HttpContext ctx) =>
        {
            var access = await ctx.GetTokenAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, "access_token");
            var refresh = await ctx.GetTokenAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, "refresh_token");
            var expires = await ctx.GetTokenAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, "expires_at");
            return Results.Json(new { AccessToken = access, RefreshToken = refresh, ExpiresAt = expires });
        }).AllowAnonymous();
        // Test-only: signs the caller into the session scheme with no tokens stored, the scenario the BFF
        // proxy's request transform must tolerate -- a ticket that predates ADR 0027, or one OpenIddict
        // never captured a token for. Not reachable through the real login flow, which always carries
        // whatever token OpenEMR issued.
        app.MapGet("/test/sign-in-without-tokens", async (HttpContext ctx) =>
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-subject")],
                OpenIddictClientServiceCollectionExtensions.SessionScheme);
            await ctx.SignInAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, new ClaimsPrincipal(identity));
            return Results.Ok();
        }).AllowAnonymous();
        // Test-only: reports what the forwarded-headers middleware (ADR 0039) decided about this request,
        // the same thing a cookie's CookieSecurePolicy.SameAsRequest reads to decide Secure.
        app.MapGet("/test/is-https", (HttpContext ctx) => Results.Json(new { ctx.Request.IsHttps }))
            .AllowAnonymous();
        await app.StartAsync();

        return new TestApi(app);
    }

    /// <summary>
    /// Asks the OS for a free loopback port and releases it immediately, for a caller that needs to know its
    /// <see cref="StartAsync"/> port before calling it (to build a matching <c>OpenEmr:RedirectUri</c>). A
    /// small, standard race exists between releasing it here and <see cref="StartAsync"/> rebinding it.
    /// </summary>
    public static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        BrowserClient.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
