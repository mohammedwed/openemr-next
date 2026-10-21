using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="PasswordLoginHandler"/> with a fake identity provider and token
/// issuer. Each test checks how an OpenEMR outcome maps to a <see cref="LoginResult"/>, and
/// whether a token was issued.
/// </summary>
public class PasswordLoginHandlerTests
{
    private readonly FakeOpenEmrIdentityProvider _identityProvider = new();
    private readonly FakeAccessTokenIssuer _tokenIssuer = new();

    private PasswordLoginHandler CreateHandler() => new(_identityProvider, _tokenIssuer);

    /// <summary>A valid sample username, used by most tests.</summary>
    private static Username Username() => new("alice_01");

    /// <summary>A valid sample password, used by most tests.</summary>
    private static Password Password() => new("Passw0rd!");

    [Fact]
    public async Task Accepted_credentials_issue_a_token_for_the_openemr_subject()
    {
        _identityProvider.Result = new PasswordGrantResult(200, "{}", "subject-123");

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        var success = Assert.IsType<LoginSucceeded>(result);
        Assert.Equal("fake-token", success.Token.AccessToken);
        Assert.Equal(1800, success.Token.ExpiresInSeconds);
        Assert.Equal("subject-123", _tokenIssuer.IssuedForSubject);
        Assert.Equal("alice_01", _tokenIssuer.IssuedForName);
    }

    [Fact]
    public async Task Rejected_credentials_return_openemr_status_and_body_without_issuing_a_token()
    {
        const string body = "{\"error\":\"invalid_grant\"}";
        _identityProvider.Result = new PasswordGrantResult(401, body, null);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        var rejected = Assert.IsType<LoginRejected>(result);
        Assert.Equal(401, rejected.StatusCode);
        Assert.Equal(body, rejected.Body);
        Assert.Null(_tokenIssuer.IssuedForSubject);
    }

    [Fact]
    public async Task Unreachable_identity_provider_returns_unavailable()
    {
        _identityProvider.Exception = new IdentityProviderUnavailableException(new HttpRequestException());

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUnavailable>(result);
        Assert.Null(_tokenIssuer.IssuedForSubject);
    }

    [Fact]
    public async Task Passes_the_same_value_objects_to_the_identity_provider()
    {
        var username = Username();
        var password = Password();
        _identityProvider.Result = new PasswordGrantResult(401, "{}", null);

        await CreateHandler().HandleAsync(username, password, CancellationToken.None);

        Assert.Same(username, _identityProvider.ReceivedUsername);
        Assert.Same(password, _identityProvider.ReceivedPassword);
    }

    [Fact]
    public async Task Handler_does_not_consume_the_password()
    {
        var password = Password();
        _identityProvider.Result = new PasswordGrantResult(401, "{}", null);

        await CreateHandler().HandleAsync(Username(), password, CancellationToken.None);

        // Still available: only the identity provider may take the plaintext.
        Assert.Equal("Passw0rd!", password.TakeValue());
    }

    [Fact]
    public async Task Malformed_upstream_response_returns_upstream_error()
    {
        _identityProvider.Exception = new IdentityProviderResponseException("bad body");

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUpstreamError>(result);
        Assert.Null(_tokenIssuer.IssuedForSubject);
    }
}
