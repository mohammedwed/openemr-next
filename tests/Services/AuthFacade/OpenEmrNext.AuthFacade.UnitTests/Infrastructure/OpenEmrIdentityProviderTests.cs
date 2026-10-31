using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="OpenEmrIdentityProvider"/> over a fake HTTP handler. They check
/// the password-grant request it sends, how it reads the token fields from OpenEMR's success response, and how it
/// maps transport failures, timeouts and unusable success responses to exceptions.
/// </summary>
public class OpenEmrIdentityProviderTests
{
    private readonly FakeHttpMessageHandler _handler = new();

    /// <summary>Creates a provider wired to the fake handler with the sample client settings.</summary>
    private OpenEmrIdentityProvider CreateProvider()
    {
        var http = new HttpClient(_handler) { BaseAddress = new Uri("https://openemr.test/") };
        var options = Options.Create(new OpenEmrOptions
        {
            BaseUrl = "https://openemr.test",
            ClientId = "facade-client",
            ClientSecret = "facade-secret",
            Scope = "openid api:oemr"
        });

        return new OpenEmrIdentityProvider(http, options);
    }

    private static Username Username() => new("alice_01");
    private static Password Password() => new("Passw0rd!");

    /// <summary>Sets the fake handler to return a JSON response with the given status and body.</summary>
    private void RespondWith(HttpStatusCode status, string body)
    {
        _handler.Response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task Accepted_credentials_return_openemr_body_unchanged()
    {
        const string body = "{\"access_token\":\"not-a-jwt-to-this-provider\",\"token_type\":\"Bearer\",\"expires_in\":3600,\"refresh_token\":\"r-1\"}";
        RespondWith(HttpStatusCode.OK, body);

        var result = await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("not-a-jwt-to-this-provider", result.Token!.AccessToken);
        Assert.Equal("Bearer", result.Token.TokenType);
        Assert.Equal(3600, result.Token.ExpiresIn);
        Assert.Equal("r-1", result.Token.RefreshToken);
    }

    [Fact]
    public async Task Success_without_token_type_defaults_to_bearer_and_omits_optional_fields()
    {
        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"t\"}");

        var result = await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal("Bearer", result.Token!.TokenType);
        Assert.Null(result.Token.ExpiresIn);
        Assert.Null(result.Token.RefreshToken);
    }

    [Fact]
    public async Task Sends_the_password_grant_with_the_facade_client_and_scope()
    {
        RespondWith(HttpStatusCode.Unauthorized, "{}");

        await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, _handler.Request!.Method);
        Assert.Equal("oauth2/default/token", _handler.Request.RequestUri!.AbsolutePath.TrimStart('/'));

        var form = QueryHelpers.ParseQuery(_handler.RequestBody);
        Assert.Equal("password", form["grant_type"]);
        Assert.Equal("facade-client", form["client_id"]);
        Assert.Equal("facade-secret", form["client_secret"]);
        Assert.Equal("users", form["user_role"]);
        Assert.Equal("openid api:oemr", form["scope"]);
        Assert.Equal("alice_01", form["username"]);
    }

    [Fact]
    public async Task Sends_the_plaintext_password_and_consumes_it()
    {
        RespondWith(HttpStatusCode.Unauthorized, "{}");
        var password = Password();

        await CreateProvider().PasswordGrantAsync(Username(), password, CancellationToken.None);

        var form = QueryHelpers.ParseQuery(_handler.RequestBody);
        Assert.Equal("Passw0rd!", form["password"]);
        Assert.Throws<InvalidOperationException>(() => password.TakeValue());
    }

    [Fact]
    public async Task Rejected_credentials_pass_through_openemr_status_and_body()
    {
        RespondWith(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}");

        var result = await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task Unreachable_openemr_throws_identity_provider_unavailable()
    {
        _handler.Exception = new HttpRequestException("connection refused");

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));

        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Client_timeout_throws_identity_provider_unavailable()
    {
        _handler.Exception = new TaskCanceledException("request timed out", new TimeoutException());

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));

        Assert.IsType<TaskCanceledException>(ex.InnerException);
    }

    [Fact]
    public async Task Success_without_access_token_throws_identity_provider_response_exception()
    {
        RespondWith(HttpStatusCode.OK, "{\"token_type\":\"Bearer\"}");

        await Assert.ThrowsAsync<IdentityProviderResponseException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));
    }

    [Fact]
    public async Task Success_with_an_empty_access_token_throws_identity_provider_response_exception()
    {
        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"  \"}");

        await Assert.ThrowsAsync<IdentityProviderResponseException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));
    }

    [Fact]
    public async Task Success_with_a_non_json_body_throws_identity_provider_response_exception()
    {
        RespondWith(HttpStatusCode.OK, "<html>maintenance</html>");

        await Assert.ThrowsAsync<IdentityProviderResponseException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));
    }

    [Fact]
    public async Task Authorization_code_grant_sends_the_code_verifier_and_redirect_uri()
    {
        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"t\"}");

        await CreateProvider().AuthorizationCodeGrantAsync(
            "auth-code", "verifier-1", "https://facade.test/callback", CancellationToken.None);

        var form = QueryHelpers.ParseQuery(_handler.RequestBody);
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("facade-client", form["client_id"]);
        Assert.Equal("facade-secret", form["client_secret"]);
        Assert.Equal("auth-code", form["code"]);
        Assert.Equal("verifier-1", form["code_verifier"]);
        Assert.Equal("https://facade.test/callback", form["redirect_uri"]);
    }

    [Fact]
    public async Task Authorization_code_grant_reads_the_id_token()
    {
        const string body = "{\"access_token\":\"t\",\"id_token\":\"id-1\"}";
        RespondWith(HttpStatusCode.OK, body);

        var result = await CreateProvider().AuthorizationCodeGrantAsync(
            "auth-code", "verifier-1", "https://facade.test/callback", CancellationToken.None);

        Assert.Equal("id-1", result.Token!.IdToken);
    }

    [Fact]
    public async Task Password_grant_response_without_an_id_token_leaves_it_null()
    {
        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"t\"}");

        var result = await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Null(result.Token!.IdToken);
    }

    [Fact]
    public async Task Rejected_authorization_code_passes_through_openemr_status()
    {
        RespondWith(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}");

        var result = await CreateProvider().AuthorizationCodeGrantAsync(
            "auth-code", "verifier-1", "https://facade.test/callback", CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Null(result.Token);
    }
}
