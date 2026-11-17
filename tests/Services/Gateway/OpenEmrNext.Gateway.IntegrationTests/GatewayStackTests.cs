using System.Net;
using Xunit;

namespace OpenEmrNext.Gateway.IntegrationTests;

/// <summary>
/// Smoke tests against the running gateway container, verifying the behavior ADR 0033/0039 built:
/// routing AuthFacade's path prefixes to AuthFacade and everything else to WebApp, terminating TLS,
/// redirecting plain HTTP, and sending HSTS. They do not exercise AuthFacade's own login flow —
/// that is <c>AuthFacade.IntegrationTests</c>' job. They check that the services holding clinical and
/// patient data refuse a request with no session (ADR 0075); the signed-in half of that check needs a real
/// OpenEMR login, so it is covered by <c>OpenEmrNext.Gateway.ProxyTests</c> against a fake sign-in service.
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

    [Theory]
    [InlineData("/api/v1/patients")]
    [InlineData("/fhir/Patient")]
    public async Task PatientManagement_route_prefixes_need_a_session_and_are_not_served_by_WebApp(string path)
    {
        var response = await Http.GetAsync(GatewayStackConfig.GatewayHttpsBaseUrl + path);

        // No session cookie: the gateway answers 401 itself and forwards nothing (ADR 0075). Before this check the gateway
        // attached the service's secret to every request, so an anonymous caller got a 200 and the whole patient list.
        // A 401 that is JSON (not WebApp's 200-HTML SPA fallback) shows the route was matched and the check ran.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("GET", "/api/v1/encounters")]
    [InlineData("POST", "/api/v1/encounters")]
    [InlineData("GET", "/api/v1/problems")]
    [InlineData("GET", "/api/v1/allergies")]
    [InlineData("GET", "/api/v1/immunizations")]
    [InlineData("GET", "/api/v1/lookups/facilities")]
    public async Task ClinicalCare_route_prefixes_need_a_session_and_a_forged_identity_does_not_stand_in_for_one(string method, string path)
    {
        // An empty body would be a 400 from ClinicalCare, so a 401 proves the request never left the gateway. The forged header is
        // what an attacker would try (ADR 0075): the gateway removes it, and without a session there is nothing to put in its place.
        using var request = new HttpRequestMessage(new HttpMethod(method), GatewayStackConfig.GatewayHttpsBaseUrl + path);
        request.Headers.Add("X-Authenticated-User", Guid.NewGuid().ToString());
        if (method == "POST")
        {
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        var response = await Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_cookie_that_is_not_a_session_is_refused_not_passed_through()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GatewayStackConfig.GatewayHttpsBaseUrl + "/api/v1/encounters");
        request.Headers.Add("Cookie", ".AspNetCore.AuthFacadeSession=not-a-real-session");

        var response = await Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
