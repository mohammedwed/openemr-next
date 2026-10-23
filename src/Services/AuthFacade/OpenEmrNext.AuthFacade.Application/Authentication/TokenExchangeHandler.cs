using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

/// <summary>
/// Handles an RFC 8693 token exchange: verifies an OpenEMR-issued JWT and, when it is valid,
/// issues this service's own access token for the same subject.
/// </summary>
public sealed class TokenExchangeHandler(IExternalTokenVerifier verifier, IAccessTokenIssuer tokenIssuer)
{
    /// <summary>Exchanges an external token for this service's token.</summary>
    /// <param name="subjectToken">The compact JWT presented as <c>subject_token</c>.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// <see cref="TokenExchangeSucceeded"/> when the token verifies, <see cref="TokenExchangeRejected"/>
    /// when it does not, or <see cref="TokenExchangeUnavailable"/> when its keys cannot be fetched.
    /// </returns>
    public async Task<TokenExchangeResult> HandleAsync(string subjectToken, CancellationToken ct)
    {
        VerifiedToken? verified;
        try
        {
            verified = await verifier.VerifyAsync(subjectToken, ct);
        }
        catch (TokenVerifierUnavailableException)
        {
            return new TokenExchangeUnavailable();
        }

        if (verified is null)
        {
            return new TokenExchangeRejected();
        }

        // OpenEMR's token carries no name claim in this stack, so the issued token has an empty name.
        return new TokenExchangeSucceeded(tokenIssuer.Issue(verified.Subject, string.Empty));
    }
}

/// <summary>The outcome of a token exchange.</summary>
public abstract record TokenExchangeResult;

/// <summary>The external token verified, and this service issued its own token.</summary>
/// <param name="Token">The issued access token.</param>
public sealed record TokenExchangeSucceeded(IssuedAccessToken Token) : TokenExchangeResult;

/// <summary>The external token did not verify.</summary>
public sealed record TokenExchangeRejected : TokenExchangeResult;

/// <summary>The keys needed to verify the token could not be fetched.</summary>
public sealed record TokenExchangeUnavailable : TokenExchangeResult;
