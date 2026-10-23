using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.UnitTests.Application;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the HTTP surface (<c>/hello</c>, <c>/connect/token</c>, <c>/me</c>) in-process through
/// <see cref="TestApi"/>. Login tests set the fake identity provider's result or exception first,
/// then check the status code and body the API returns. No Docker stack is needed.
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

    /// <summary>Parses the response body as JSON. The caller disposes the returned document.</summary>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Hello_is_anonymous()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Accepted_login_returns_a_bearer_token()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, "{}", "subject-1");

        using var response = await api.Client.PostAsync("/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", body.RootElement.GetProperty("token_type").GetString());
        Assert.Equal(1800, body.RootElement.GetProperty("expires_in").GetInt32());
    }

    [Fact]
    public async Task Token_from_login_identifies_the_openemr_subject_on_me()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, "{}", "subject-1");

        using var login = await api.Client.PostAsync("/connect/token", PasswordGrant("alice_01", "Passw0rd!"));
        using var loginBody = await ReadJsonAsync(login);
        var token = loginBody.RootElement.GetProperty("access_token").GetString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var me = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var meBody = await ReadJsonAsync(me);
        Assert.Equal("subject-1", meBody.RootElement.GetProperty("subject").GetString());
        Assert.Equal("alice_01", meBody.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Me_rejects_requests_without_a_token()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_rejects_a_token_it_did_not_issue()
    {
        await using var api = await TestApi.StartAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        using var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_grant_type_returns_400()
    {
        await using var api = await TestApi.StartAsync();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["username"] = "alice_01",
            ["password"] = "Passw0rd!"
        });

        using var response = await api.Client.PostAsync("/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("unsupported_grant_type", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Missing_credentials_return_invalid_request()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.PostAsync("/connect/token", PasswordGrant("", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Short_legacy_username_reaches_openemr_instead_of_being_rejected_locally()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(401, "{\"error\":\"invalid_grant\"}", null);

        using var response = await api.Client.PostAsync("/connect/token", PasswordGrant("a", "x"));

        Assert.Equal("a", api.IdentityProvider.ReceivedUsername!.Value);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenEmr_rejection_passes_through_status_and_body()
    {
        await using var api = await TestApi.StartAsync();
        const string body = "{\"error\":\"invalid_grant\",\"error_description\":\"bad\"}";
        api.IdentityProvider.Result = new PasswordGrantResult(401, body, null);

        using var response = await api.Client.PostAsync("/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(body, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unreachable_openemr_returns_503()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Exception = new IdentityProviderUnavailableException(new HttpRequestException());

        using var response = await api.Client.PostAsync("/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("server_error", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Malformed_openemr_success_returns_502()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Exception = new IdentityProviderResponseException("no token");

        using var response = await api.Client.PostAsync("/connect/token", PasswordGrant("alice_01", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("server_error", body.RootElement.GetProperty("error").GetString());
    }
}
