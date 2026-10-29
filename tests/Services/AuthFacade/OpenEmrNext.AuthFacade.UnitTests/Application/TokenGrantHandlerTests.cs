using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="TokenGrantHandler"/> with a fake identity provider and token verifier.
/// Each test checks how an OpenEMR outcome maps to a <see cref="LoginResult"/>. A token is returned
/// only when it verifies. The status-mapping cases are exercised through the password grant; the
/// refresh grant's own tests check that it reaches the same shared logic and passes its own value object through.
/// </summary>
public class TokenGrantHandlerTests
{
    private const string OpenEmrAccessToken = "openemr-token";

    private static readonly OpenEmrToken Token = new(OpenEmrAccessToken, "Bearer", 3600, "r-1");

    private readonly FakeIdentityProvider _identityProvider = new();
    private readonly FakeExternalTokenVerifier _verifier = new();

    private TokenGrantHandler CreateHandler() => new(_identityProvider, _verifier);

    /// <summary>A valid sample username, used by most tests.</summary>
    private static Username Username() => new("alice_01");

    /// <summary>A valid sample password, used by most tests.</summary>
    private static Password Password() => new("Passw0rd!");

    /// <summary>A valid sample refresh token, used by the refresh grant's own tests.</summary>
    private static RefreshToken RefreshToken() => new("r-1");

    [Fact]
    public async Task Accepted_credentials_with_a_verified_token_return_the_openemr_token_fields()
    {
        _identityProvider.Result = new PasswordGrantResult(200, Token);
        _verifier.Result = new VerifiedToken("subject-1", null);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        var success = Assert.IsType<LoginSucceeded>(result);
        Assert.Equal(Token, success.Token);
    }

    [Fact]
    public async Task The_openemr_access_token_is_verified_before_the_login_succeeds()
    {
        _identityProvider.Result = new PasswordGrantResult(200, Token);
        _verifier.Result = new VerifiedToken("subject-1", null);

        await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(OpenEmrAccessToken, _verifier.ReceivedToken);
    }

    [Fact]
    public async Task Token_that_fails_verification_returns_upstream_error()
    {
        _identityProvider.Result = new PasswordGrantResult(200, Token);
        _verifier.Result = null;

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUpstreamError>(result);
    }

    [Fact]
    public async Task Verifier_without_reachable_keys_returns_unavailable()
    {
        _identityProvider.Result = new PasswordGrantResult(200, Token);
        _verifier.Exception = new TokenVerifierUnavailableException(new HttpRequestException());

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUnavailable>(result);
    }

    [Fact]
    public async Task Success_without_a_token_returns_upstream_error_without_verifying()
    {
        _identityProvider.Result = new PasswordGrantResult(200, null);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUpstreamError>(result);
        Assert.Null(_verifier.ReceivedToken);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Client_error_from_openemr_returns_rejected_without_verifying(int status)
    {
        _identityProvider.Result = new PasswordGrantResult(status);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginRejected>(result);
        Assert.Null(_verifier.ReceivedToken);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(408)]
    public async Task Server_or_timeout_status_from_openemr_returns_upstream_error(int status)
    {
        _identityProvider.Result = new PasswordGrantResult(status);

        var result = await CreateHandler().HandleAsync(Username(), Password(), CancellationToken.None);

        Assert.IsType<LoginUpstreamError>(result);
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
        _identityProvider.Result = new PasswordGrantResult(401);

        await CreateHandler().HandleAsync(username, password, CancellationToken.None);

        Assert.Same(username, _identityProvider.ReceivedUsername);
        Assert.Same(password, _identityProvider.ReceivedPassword);
    }

    [Fact]
    public async Task Handler_does_not_consume_the_password()
    {
        var password = Password();
        _identityProvider.Result = new PasswordGrantResult(401);

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

    [Fact]
    public async Task Accepted_refresh_token_with_a_verified_token_returns_the_openemr_token_fields()
    {
        _identityProvider.Result = new PasswordGrantResult(200, Token);
        _verifier.Result = new VerifiedToken("subject-1", null);

        var result = await CreateHandler().HandleAsync(RefreshToken(), CancellationToken.None);

        var success = Assert.IsType<LoginSucceeded>(result);
        Assert.Equal(Token, success.Token);
    }

    [Fact]
    public async Task Refused_refresh_token_returns_rejected_without_verifying()
    {
        _identityProvider.Result = new PasswordGrantResult(400);

        var result = await CreateHandler().HandleAsync(RefreshToken(), CancellationToken.None);

        Assert.IsType<LoginRejected>(result);
        Assert.Null(_verifier.ReceivedToken);
    }

    [Fact]
    public async Task Passes_the_same_refresh_token_to_the_identity_provider()
    {
        var refreshToken = RefreshToken();
        _identityProvider.Result = new PasswordGrantResult(401);

        await CreateHandler().HandleAsync(refreshToken, CancellationToken.None);

        Assert.Same(refreshToken, _identityProvider.ReceivedRefreshToken);
    }

    [Fact]
    public async Task Handler_does_not_consume_the_refresh_token()
    {
        var refreshToken = RefreshToken();
        _identityProvider.Result = new PasswordGrantResult(401);

        await CreateHandler().HandleAsync(refreshToken, CancellationToken.None);

        // Still available: only the identity provider may take the plaintext.
        Assert.Equal("r-1", refreshToken.TakeValue());
    }
}
