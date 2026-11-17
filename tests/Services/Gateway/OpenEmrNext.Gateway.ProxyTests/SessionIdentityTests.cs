using System.Net;

namespace OpenEmrNext.Gateway.ProxyTests;

/// <summary>
/// ADR 0075 against the real gateway, proxying to fake backends over real HTTP: a protected route needs a valid session, the verified user (and only the verified user) reaches the service, and anything
/// the gateway cannot check is refused rather than passed through.
/// </summary>
[Collection("Gateway")]
public class SessionIdentityTests
{
    private static readonly Guid User = Guid.Parse("9e2c4f10-0000-4000-8000-0000000000aa");
    private static string Valid => $"session=ok-{User}";

    public static IEnumerable<object[]> ProtectedPaths =>
    [
        ["/api/v1/encounters"], ["/api/v1/encounters/6c0c0c0c-0000-4000-8000-000000000001/vitals"], ["/api/v1/problems"], ["/api/v1/allergies"], ["/api/v1/immunizations"],
        ["/api/v1/lookups/facilities"], ["/api/v1/patients"], ["/api/v1/patients/abc/insurance"], ["/fhir/Patient"]
    ];

    [Theory]
    [MemberData(nameof(ProtectedPaths))]
    public async Task A_protected_route_without_a_session_is_401_and_never_reaches_a_backend(string path)
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(gateway.ClinicalCare.Requests);
        Assert.Empty(gateway.PatientManagement.Requests);
        Assert.Empty(gateway.AuthFacade.Requests);   // no cookie, so not even worth asking
    }

    [Theory]
    [InlineData("session=revoked")]
    [InlineData("session=odd")]
    [InlineData("other=cookie")]
    public async Task A_refused_expired_or_unusable_session_is_401_and_never_reaches_a_backend(string cookie)
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", cookie));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(gateway.ClinicalCare.Requests);
    }

    [Fact]
    public async Task A_valid_session_is_forwarded_to_clinical_care_with_the_verified_user_its_own_secret_and_no_cookie()
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/immunizations?patientUuid=x", Valid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seen = Assert.Single(gateway.ClinicalCare.Requests);
        Assert.Equal(User.ToString(), seen.Headers["X-Authenticated-User"]);
        Assert.Equal(GatewayHarness.ClinicalSecret, seen.Headers["X-Internal-Gateway-Secret"]);
        Assert.False(seen.Headers.ContainsKey("Cookie"), "the session cookie must not reach the data services");
        Assert.Empty(gateway.PatientManagement.Requests);
    }

    [Theory]
    [InlineData("/api/v1/patients")]
    [InlineData("/fhir/Patient")]
    public async Task A_valid_session_is_forwarded_to_patient_management_with_the_verified_user_and_its_own_secret(string path)
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, path, Valid));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var seen = Assert.Single(gateway.PatientManagement.Requests);
        Assert.Equal(User.ToString(), seen.Headers["X-Authenticated-User"]);
        Assert.Equal(GatewayHarness.PatientSecret, seen.Headers["X-Internal-Gateway-Secret"]);
        Assert.False(seen.Headers.ContainsKey("Cookie"));
        Assert.Empty(gateway.ClinicalCare.Requests);
    }

    [Fact]
    public async Task A_caller_supplied_identity_header_is_replaced_by_the_verified_one_and_cannot_stand_in_for_a_session()
    {
        await using var gateway = await GatewayHarness.StartAsync();
        var spoof = new Dictionary<string, string> { ["X-Authenticated-User"] = Guid.NewGuid().ToString() };

        var anonymous = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", headers: spoof));
        var signedIn = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", Valid, spoof));

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.Equal(User.ToString(), Assert.Single(gateway.ClinicalCare.Requests).Headers["X-Authenticated-User"]);
    }

    [Fact]
    public async Task The_identity_header_is_removed_on_every_route_so_no_backend_is_ever_handed_one_the_gateway_did_not_verify()
    {
        await using var gateway = await GatewayHarness.StartAsync();
        var spoof = new Dictionary<string, string> { ["X-Authenticated-User"] = Guid.NewGuid().ToString() };

        await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/health", headers: spoof));
        await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/some-dashboard-route", headers: spoof));

        Assert.False(Assert.Single(gateway.AuthFacade.Requests).Headers.ContainsKey("X-Authenticated-User"));
        Assert.False(Assert.Single(gateway.WebApp.Requests).Headers.ContainsKey("X-Authenticated-User"));
    }

    [Fact]
    public async Task When_the_sign_in_service_errors_the_gateway_fails_closed_with_503_and_forwards_nothing()
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", "session=boom"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.Contains("Retry-After"));
        Assert.Empty(gateway.ClinicalCare.Requests);
    }

    [Fact]
    public async Task When_the_sign_in_service_cannot_be_reached_the_gateway_fails_closed_with_503()
    {
        await using var gateway = await GatewayHarness.StartAsync(authFacadeReachable: false);

        var response = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/problems", Valid));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(gateway.ClinicalCare.Requests);
    }

    [Fact]
    public async Task The_check_sends_the_callers_cookie_to_sign_in_service_and_nothing_else_of_theirs()
    {
        await using var gateway = await GatewayHarness.StartAsync();
        var extra = new Dictionary<string, string> { ["Authorization"] = "Bearer secret-token", ["X-Custom"] = "x" };

        await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", Valid, extra));

        var check = Assert.Single(gateway.AuthFacade.Requests);
        Assert.Equal("/api/v1/me", check.Path);
        Assert.Equal(Valid, check.Headers["Cookie"]);
        Assert.False(check.Headers.ContainsKey("Authorization"));
        Assert.False(check.Headers.ContainsKey("X-Custom"));
    }

    [Fact]
    public async Task The_path_match_ignores_case_so_a_different_spelling_cannot_slip_past_the_check()
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.GetAsync("/API/V1/ENCOUNTERS");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(gateway.ClinicalCare.Requests);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/me")]
    [InlineData("/oidc/login")]
    [InlineData("/some-dashboard-route")]
    public async Task Sign_in_itself_and_the_static_app_need_no_session_and_are_not_checked(string path)
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var response = await gateway.Client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(gateway.ClinicalCare.Requests);
        Assert.Empty(gateway.PatientManagement.Requests);
        // The only thing AuthFacade ever saw for these is the request itself, never a separate session check.
        Assert.All(gateway.AuthFacade.Requests, r => Assert.Equal(path, r.Path));
    }

    [Fact]
    public async Task Every_request_is_checked_afresh_so_a_revoked_session_stops_working_at_once()
    {
        await using var gateway = await GatewayHarness.StartAsync();

        var first = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", Valid));
        var revoked = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", "session=revoked"));
        var second = await gateway.Client.SendAsync(gateway.Request(HttpMethod.Get, "/api/v1/encounters", Valid));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.OK], [first.StatusCode, revoked.StatusCode, second.StatusCode]);
        Assert.Equal(3, gateway.AuthFacade.Requests.Count);
    }
}

/// <summary>The harness sets process-wide environment variables, so these tests must not run in parallel with each other.</summary>
[CollectionDefinition("Gateway", DisableParallelization = true)]
public class GatewayCollection;
