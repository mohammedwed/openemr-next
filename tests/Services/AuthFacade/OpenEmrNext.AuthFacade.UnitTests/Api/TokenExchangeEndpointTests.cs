using System.Net;
using System.Text.Json;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the RFC 8693 token exchange on <c>POST /connect/token</c>, in-process through
/// <see cref="TestApi"/>. The fake verifier decides whether the subject token is valid.
/// </summary>
public class TokenExchangeEndpointTests
{
    private const string TokenExchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string JwtTokenType = "urn:ietf:params:oauth:token-type:jwt";

    /// <summary>Builds a form-encoded token exchange request.</summary>
    private static FormUrlEncodedContent Exchange(string? subjectToken, string? subjectTokenType = JwtTokenType)
    {
        var form = new Dictionary<string, string> { ["grant_type"] = TokenExchangeGrant };
        if (subjectToken is not null)
        {
            form["subject_token"] = subjectToken;
        }

        if (subjectTokenType is not null)
        {
            form["subject_token_type"] = subjectTokenType;
        }

        return new FormUrlEncodedContent(form);
    }

    /// <summary>Parses the response body as JSON. The caller disposes the returned document.</summary>
    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Verified_subject_token_is_exchanged_for_a_facade_token()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Result = new VerifiedToken("subject-1");

        using var response = await api.Client.PostAsync("/connect/token", Exchange("external-jwt"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("access_token").GetString()));
        Assert.Equal("urn:ietf:params:oauth:token-type:access_token", body.RootElement.GetProperty("issued_token_type").GetString());
        Assert.Equal("Bearer", body.RootElement.GetProperty("token_type").GetString());
        Assert.Equal(1800, body.RootElement.GetProperty("expires_in").GetInt32());
    }

    [Fact]
    public async Task Exchange_does_not_call_openemr_for_a_password_check()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Result = new VerifiedToken("subject-1");

        using var response = await api.Client.PostAsync("/connect/token", Exchange("external-jwt"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(api.IdentityProvider.ReceivedUsername);
    }

    [Fact]
    public async Task Subject_token_that_does_not_verify_returns_invalid_grant()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Result = null;

        using var response = await api.Client.PostAsync("/connect/token", Exchange("forged-jwt"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Missing_subject_token_returns_invalid_request()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.PostAsync("/connect/token", Exchange(subjectToken: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Subject_token_of_another_type_returns_invalid_request()
    {
        await using var api = await TestApi.StartAsync();

        using var response = await api.Client.PostAsync(
            "/connect/token",
            Exchange("opaque-token", subjectTokenType: "urn:ietf:params:oauth:token-type:refresh_token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("invalid_request", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Unavailable_issuer_keys_return_503()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Exception = new TokenVerifierUnavailableException(new HttpRequestException());

        using var response = await api.Client.PostAsync("/connect/token", Exchange("external-jwt"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("server_error", body.RootElement.GetProperty("error").GetString());
    }
}
