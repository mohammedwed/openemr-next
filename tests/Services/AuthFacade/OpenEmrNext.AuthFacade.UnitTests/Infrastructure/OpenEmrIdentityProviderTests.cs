using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="OpenEmrIdentityProvider"/> over a fake HTTP handler. They check
/// the password-grant request it sends, how it passes OpenEMR's responses through, and how it
/// maps transport failures and malformed success responses to exceptions.
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

    /// <summary>
    /// Builds a JWT with the given subject, shaped like an OpenEMR access token. It is signed with a
    /// throwaway key, which is fine because the provider reads the subject without verifying the signature.
    /// </summary>
    private static string CreateOpenEmrToken(string subject)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-only-key-for-openemr-token-fake-32b"));
        var token = new JwtSecurityToken(
            claims: [new Claim(JwtRegisteredClaimNames.Sub, subject)],
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
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
    public async Task Accepted_credentials_return_the_subject_from_openemr_token()
    {
        var token = CreateOpenEmrToken("subject-123");
        RespondWith(HttpStatusCode.OK, $"{{\"access_token\":\"{token}\"}}");

        var result = await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("subject-123", result.Subject);
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
        const string body = "{\"error\":\"invalid_grant\"}";
        RespondWith(HttpStatusCode.Unauthorized, body);

        var result = await CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
        Assert.Equal(body, result.Body);
        Assert.Null(result.Subject);
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
    public async Task Success_without_access_token_throws_identity_provider_response_exception()
    {
        RespondWith(HttpStatusCode.OK, "{\"token_type\":\"Bearer\"}");

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
    public async Task Success_with_an_unreadable_access_token_throws_identity_provider_response_exception()
    {
        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"not-a-jwt\"}");

        await Assert.ThrowsAsync<IdentityProviderResponseException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));
    }

    [Fact]
    public async Task Success_with_a_token_that_has_no_subject_throws_identity_provider_response_exception()
    {
        var token = CreateOpenEmrToken("");
        RespondWith(HttpStatusCode.OK, $"{{\"access_token\":\"{token}\"}}");

        await Assert.ThrowsAsync<IdentityProviderResponseException>(
            () => CreateProvider().PasswordGrantAsync(Username(), Password(), CancellationToken.None));
    }
}
