using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.UnitTests.Application;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the HTTP surface (<c>/health</c>, <c>/api/v1/connect/token</c>, <c>/api/v1/me</c>) in-process through
/// <see cref="TestApi"/>. Login tests set the fake identity provider's result or exception first,
/// then check the status code and body the API returns. <c>/api/v1/me</c> tests set the fake verifier's result.
/// No Docker stack is needed.
/// </summary>
public class ApiEndpointTests
{
    /// <summary>Builds a form-encoded password grant body.</summary>
    private static FormUrlEncodedContent PasswordGrant(string username, string password) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "password",
        ["username"] = username,
        ["password"] = password
    });

    /// <summary>The token OpenEMR issues for an accepted login. The facade returns these fields in its own response.</summary>
    private static readonly OpenEmrToken OpenEmrTokenFields = new("openemr-token", "Bearer", 3600, "r-1");

    /// <summary>Parses the response body as JSON. The caller disposes the returned document.</summary>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Hello_is_anonymous()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_no_longer_served_under_api()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accepted_login_returns_the_facade_token_response_with_the_four_fields()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, OpenEmrTokenFields);
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null);

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        var fields = body.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "access_token", "expires_in", "refresh_token", "token_type" }, fields);
        Assert.Equal("openemr-token", body.RootElement.GetProperty("access_token").GetString());
        Assert.Equal("Bearer", body.RootElement.GetProperty("token_type").GetString());
        Assert.Equal(3600, body.RootElement.GetProperty("expires_in").GetInt32());
        Assert.Equal("r-1", body.RootElement.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task Omits_expires_in_and_refresh_token_when_openemr_does_not_return_them()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, new OpenEmrToken("openemr-token", "Bearer", null, null));
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null);

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        using var body = await ReadJsonAsync(response);
        Assert.False(body.RootElement.TryGetProperty("expires_in", out _));
        Assert.False(body.RootElement.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task Token_from_login_is_verified_on_me_and_identifies_the_subject()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, OpenEmrTokenFields);
        api.TokenVerifier.Result = new VerifiedToken("subject-1", "alice_01");

        using var login = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));
        using var loginBody = await ReadJsonAsync(login);
        var token = loginBody.RootElement.GetProperty("access_token").GetString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var me = await api.Client.SendAsync(request);

        Assert.Equal("openemr-token", api.TokenVerifier.ReceivedToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meBody = await ReadJsonAsync(me);
        Assert.Equal("subject-1", meBody.RootElement.GetProperty("subject").GetString());
        Assert.Equal("alice_01", meBody.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Me_reports_a_null_name_when_the_token_has_none()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "openemr-token");

        using var me = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meBody = await ReadJsonAsync(me);
        Assert.Equal(JsonValueKind.Null, meBody.RootElement.GetProperty("name").ValueKind);
    }

    [Fact]
    public async Task Me_rejects_requests_without_a_token()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_token_the_verifier_refuses()
    {
        await using var api = await TestApi.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_token_when_openemr_keys_are_unavailable()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Exception = new TokenVerifierUnavailableException(new HttpRequestException());
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "openemr-token");

        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_grant_type_returns_invalid_request()
    {
        await using var api = await TestApi.StartAsync();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["username"] = "alice_01",
            ["password"] = "Passw0rd!"
        });

        using var response = await api.Client.PostAsync("/api/v1/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Missing_grant_type_returns_invalid_request()
    {
        await using var api = await TestApi.StartAsync();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "alice_01",
            ["password"] = "Passw0rd!"
        });

        using var response = await api.Client.PostAsync("/api/v1/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Non_form_request_returns_415()
    {
        await using var api = await TestApi.StartAsync();
        using var json = new StringContent("{\"username\":\"alice_01\"}", System.Text.Encoding.UTF8, "application/json");

        using var response = await api.Client.PostAsync("/api/v1/connect/token", json);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Missing_credentials_return_invalid_request()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Short_legacy_username_reaches_openemr_instead_of_being_rejected_locally()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(400);

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("a", "x"));

        Assert.Equal("a", api.IdentityProvider.ReceivedUsername!.Value);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenEmr_rejection_returns_invalid_grant_without_openemr_body()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(400);

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
        Assert.Equal("The username or password is incorrect.", body.RootElement.GetProperty("error_description").GetString());
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task OpenEmr_server_error_returns_502_not_openemr_status()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(500);

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Unreachable_openemr_returns_503()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Exception = new IdentityProviderUnavailableException(new HttpRequestException());

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("server_error", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task OpenEmr_token_that_fails_verification_returns_502()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, OpenEmrTokenFields);
        api.TokenVerifier.Result = null;

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_openemr_success_returns_502()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Exception = new IdentityProviderResponseException("no token");

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("server_error", body.RootElement.GetProperty("error").GetString());
    }
}
