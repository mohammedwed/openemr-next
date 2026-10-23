using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.UnitTests.Application;

/// <summary>
/// Unit tests for <see cref="TokenExchangeHandler"/>. They check that only a verified token is
/// exchanged, and that verifier outcomes map to the right result type.
/// </summary>
public class TokenExchangeHandlerTests
{
    private readonly FakeExternalTokenVerifier _verifier = new();
    private readonly FakeAccessTokenIssuer _issuer = new();

    private TokenExchangeHandler CreateHandler() => new(_verifier, _issuer);

    [Fact]
    public async Task Verified_token_is_exchanged_for_a_token_for_the_same_subject()
    {
        _verifier.Result = new VerifiedToken("subject-1");

        var result = await CreateHandler().HandleAsync("external-jwt", CancellationToken.None);

        var success = Assert.IsType<TokenExchangeSucceeded>(result);
        Assert.Equal("fake-token", success.Token.AccessToken);
        Assert.Equal("subject-1", _issuer.IssuedForSubject);
    }

    [Fact]
    public async Task Exchanged_token_has_an_empty_name_because_the_external_token_carries_none()
    {
        _verifier.Result = new VerifiedToken("subject-1");

        await CreateHandler().HandleAsync("external-jwt", CancellationToken.None);

        Assert.Equal(string.Empty, _issuer.IssuedForName);
    }

    [Fact]
    public async Task Token_that_does_not_verify_is_rejected_and_nothing_is_issued()
    {
        _verifier.Result = null;

        var result = await CreateHandler().HandleAsync("external-jwt", CancellationToken.None);

        Assert.IsType<TokenExchangeRejected>(result);
        Assert.Null(_issuer.IssuedForSubject);
    }

    [Fact]
    public async Task Unavailable_keys_are_reported_and_nothing_is_issued()
    {
        _verifier.Exception = new TokenVerifierUnavailableException(new HttpRequestException());

        var result = await CreateHandler().HandleAsync("external-jwt", CancellationToken.None);

        Assert.IsType<TokenExchangeUnavailable>(result);
        Assert.Null(_issuer.IssuedForSubject);
    }

    [Fact]
    public async Task The_subject_token_is_passed_to_the_verifier_unchanged()
    {
        _verifier.Result = null;

        await CreateHandler().HandleAsync("external-jwt", CancellationToken.None);

        Assert.Equal("external-jwt", _verifier.ReceivedToken);
    }
}
