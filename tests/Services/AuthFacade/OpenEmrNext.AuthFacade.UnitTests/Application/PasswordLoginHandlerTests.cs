using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="PasswordLoginHandler"/> with a fake identity provider and token verifier.
/// Each test checks how an OpenEMR outcome maps to a <see cref="LoginResult"/>. A token is returned
/// only when it verifies.
/// </summary>
public class PasswordLoginHandlerTests
{
    private const string OpenEmrToken = "openemr-token";
    private const string OpenEmrBody = "{\"access_token\":\"openemr-token\",\"token_type\":\"Bearer\"}";

    private readonly FakeIdentityProvider _identityProvider = new();
    private readonly FakeExternalTokenVerifier _verifier = new();

    private PasswordLoginHandler CreateHandler() => new(_identityProvider, _verifier);

    /// <summary>A valid sample username, used by most tests.</summary>
    private static Username Username() => new("alice_01");

    /// <summary>A valid sample password, used by most tests.</summary>
    private static Password Password() => new("Passw0rd!");

    [Fact]
    public async Task Accepted_credentials_with_a_verified_token_return_openemr_token_response_unchanged()
    {
        _identityProvider.Result = new PasswordGrantResult(200, OpenEmrBody, OpenEmrToken);
        _verifier.Result = new VerifiedToken("subject-1", null);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        var success = Assert.IsType<LoginSucceeded>(result);
        Assert.Equal(OpenEmrBody, success.Body);
    }

    [Fact]
    public async Task The_openemr_access_token_is_verified_before_the_login_succeeds()
    {
        _identityProvider.Result = new PasswordGrantResult(200, OpenEmrBody, OpenEmrToken);
        _verifier.Result = new VerifiedToken("subject-1", null);

        await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(OpenEmrToken, _verifier.ReceivedToken);
    }

    [Fact]
    public async Task Token_that_fails_verification_returns_upstream_error()
    {
        _identityProvider.Result = new PasswordGrantResult(200, OpenEmrBody, OpenEmrToken);
        _verifier.Result = null;

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUpstreamError>(result);
    }

    [Fact]
    public async Task Verifier_without_reachable_keys_returns_unavailable()
    {
        _identityProvider.Result = new PasswordGrantResult(200, OpenEmrBody, OpenEmrToken);
        _verifier.Exception = new TokenVerifierUnavailableException(new HttpRequestException());

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUnavailable>(result);
    }

    [Fact]
    public async Task Success_without_an_access_token_returns_upstream_error_without_verifying()
    {
        _identityProvider.Result = new PasswordGrantResult(200, "{}", null);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUpstreamError>(result);
        Assert.Null(_verifier.ReceivedToken);
    }

    [Fact]
    public async Task Rejected_credentials_return_openemr_status_and_body_without_verifying()
    {
        const string body = "{\"error\":\"invalid_grant\"}";
        _identityProvider.Result = new PasswordGrantResult(401, body);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        var rejected = Assert.IsType<LoginRejected>(result);
        Assert.Equal(401, rejected.StatusCode);
        Assert.Equal(body, rejected.Body);
        Assert.Null(_verifier.ReceivedToken);
    }

    [Fact]
    public async Task Unreachable_identity_provider_returns_unavailable()
    {
        _identityProvider.Exception = new IdentityProviderUnavailableException(new HttpRequestException());

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUnavailable>(result);
    }

    [Fact]
    public async Task Passes_the_same_value_objects_to_the_identity_provider()
    {
        var username = Username();
        var password = Password();
        _identityProvider.Result = new PasswordGrantResult(401, "{}");

        await CreateHandler().HandleAsync(username, password, CancellationToken.None);

        Assert.Same(username, _identityProvider.ReceivedUsername);
        Assert.Same(password, _identityProvider.ReceivedPassword);
    }

    [Fact]
    public async Task Handler_does_not_consume_the_password()
    {
        var password = Password();
        _identityProvider.Result = new PasswordGrantResult(401, "{}");

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
    }
}
