using System.Net;
using Xunit;

namespace OpenEmrNext.Gateway.IntegrationTests;

/// <summary>
/// Smoke tests against the running gateway container, verifying the behavior ADR 0033/0039 built:
/// routing AuthFacade's path prefixes to AuthFacade and everything else to WebApp, terminating TLS,
/// redirecting plain HTTP, and sending HSTS. They do not exercise AuthFacade's own login flow —
/// that is <c>AuthFacade.IntegrationTests</c>' job — and they do not assert anything about token
/// validation, since the gateway does none yet (ADR 0048).
/// </summary>
/// <remarks>
/// Requires the stack to be up. <see cref="GatewayReadinessFixture"/> waits for it before these
/// tests run. Run them with <c>dotnet test --filter Category=Integration</c> or with the
/// integration project directly.
/// </remarks>
[Collection("GatewayStack")]
[Trait("Category", "Integration")]
public class GatewayStackTests
{
    // Dev only: the gateway uses a self-signed certificate (ADR 0039).
    private static readonly HttpClientHandler NoRedirectHandler = new()
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        AllowAutoRedirect = false
    };

    private static readonly HttpClient Http = new(NoRedirectHandler) { Timeout = TimeSpan.FromSeconds(20) };

    [Fact]
    public async Task Plain_HTTP_redirects_to_HTTPS()
    {
        var response = await Http.GetAsync(GatewayStackConfig.GatewayHttpBaseUrl + "/");

        Assert.True(response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect,
            $"Expected a redirect, got {(int)response.StatusCode}");
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("https", response.Headers.Location!.Scheme);
    }

    [Fact]
    public async Task HTTPS_responses_carry_the_HSTS_header()
    {
        var response = await Http.GetAsync(GatewayStackConfig.GatewayHttpsBaseUrl + "/health");

        Assert.True(response.Headers.TryGetValues("Strict-Transport-Security", out var values),
            "No Strict-Transport-Security header on the gateway's HTTPS response.");
        Assert.Contains("max-age=2592000", values!.First());
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/me")]
    public async Task AuthFacade_route_prefixes_are_routed_to_AuthFacade_not_WebApp(string path)
    {
        var response = await Http.GetAsync(GatewayStackConfig.GatewayHttpsBaseUrl + path);

        // AuthFacade answers every path it owns with something other than WebApp's SPA fallback.
        // /health is 200 for anyone; /api/v1/me without a session is 401 — both come from AuthFacade,
        // never a 200-with-HTML page, which is what WebApp's catch-all would return instead.
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.NotEqual("text/html", contentType);
    }

    [Fact]
    public async Task Everything_else_is_routed_to_WebApp()
    {
        var response = await Http.GetAsync(GatewayStackConfig.GatewayHttpsBaseUrl + "/some-dashboard-route");

        // WebApp is a Blazor WASM SPA served by nginx (ADR 0032): any path not claimed by one of
        // AuthFacade's own prefixes falls through to its catch-all, which serves index.html rather
        // than a 404 -- that is what makes client-side routing work at all.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
