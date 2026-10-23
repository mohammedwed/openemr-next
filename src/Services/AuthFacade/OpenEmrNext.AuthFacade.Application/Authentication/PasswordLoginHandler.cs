using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

/// <summary>
/// Handles a password login by forwarding the credentials to OpenEMR. OpenEMR's token response is
/// returned to the caller unchanged, but only after its access token verifies. This service issues no token of its own.
/// </summary>
public sealed class PasswordLoginHandler(IIdentityProvider identityProvider, IExternalTokenVerifier verifier)
{
    /// <summary>Attempts a password login.</summary>
    /// <param name="username">The validated login name.</param>
    /// <param name="password">The validated password.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// <see cref="LoginSucceeded"/> when OpenEMR accepts the credentials and its token verifies,
    /// <see cref="LoginRejected"/> when OpenEMR refuses them, <see cref="LoginUnavailable"/> when OpenEMR
    /// or its keys cannot be reached, or <see cref="LoginUpstreamError"/> when its success response is unusable
    /// or its token does not verify.
    /// </returns>
    public async Task<LoginResult> HandleAsync(Username username, Password password, CancellationToken ct)
    {
        PasswordGrantResult grant;
        try
        {
            grant = await identityProvider.PasswordGrantAsync(username, password, ct);
        }
        catch (IdentityProviderUnavailableException)
        {
            return new LoginUnavailable();
        }
        catch (IdentityProviderResponseException)
        {
            return new LoginUpstreamError();
        }

        if (grant.StatusCode != Status200OK)
        {
            return new LoginRejected(grant.StatusCode, grant.Body);
        }

        if (grant.AccessToken is null)
        {
            return new LoginUpstreamError();
        }

        // A token OpenEMR issued that does not verify is an upstream fault, not a rejected login.
        VerifiedToken? verified;
        try
        {
            verified = await verifier.VerifyAsync(grant.AccessToken, ct);
        }
        catch (TokenVerifierUnavailableException)
        {
            return new LoginUnavailable();
        }

        return verified is null
            ? new LoginUpstreamError()
            : new LoginSucceeded(grant.Body);
    }

    /// <summary>The HTTP status OpenEMR returns when it accepts the credentials.</summary>
    private const int Status200OK = 200;
}
