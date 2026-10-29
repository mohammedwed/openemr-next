using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

/// <summary>
/// Handles a password or refresh grant by forwarding it to OpenEMR.
/// OpenEMR's token is returned to the caller only after its access token verifies.
/// This service issues no token of its own.
/// </summary>
public sealed class TokenGrantHandler(IIdentityProvider identityProvider, IExternalTokenVerifier verifier)
{
    /// <summary>The HTTP status OpenEMR returns when it accepts the grant.</summary>
    private const int StatusOk = 200;

    /// <summary>The HTTP status OpenEMR returns when its request timed out.</summary>
    private const int StatusRequestTimeout = 408;

    /// <summary>Attempts a password login.</summary>
    /// <param name="username">The validated login name.</param>
    /// <param name="password">The validated password.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// <see cref="LoginSucceeded"/> when OpenEMR accepts the grant and its token verifies.
    /// <see cref="LoginRejected"/> when OpenEMR refuses it with a client error.
    /// <see cref="LoginUnavailable"/> when OpenEMR or its keys cannot be reached.
    /// <see cref="LoginUpstreamError"/> when OpenEMR fails, its success response is unusable, or its token does not verify.
    /// </returns>
    public Task<LoginResult> HandleAsync(Username username, Password password, CancellationToken ct) =>
        HandleAsync(token => identityProvider.PasswordGrantAsync(username, password, token), ct);

    /// <summary>Attempts a refresh grant.</summary>
    /// <param name="refreshToken">The validated refresh token.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The same outcomes as <see cref="HandleAsync(Username, Password, CancellationToken)"/>.</returns>
    public Task<LoginResult> HandleAsync(RefreshToken refreshToken, CancellationToken ct) =>
        HandleAsync(token => identityProvider.RefreshGrantAsync(refreshToken, token), ct);

    /// <summary>Submits the grant, maps OpenEMR's status, and verifies a successful token. Shared by both grants.</summary>
    private async Task<LoginResult> HandleAsync(
        Func<CancellationToken, Task<PasswordGrantResult>> submitGrantAsync, CancellationToken ct)
    {
        PasswordGrantResult grant;
        try
        {
            grant = await submitGrantAsync(ct);
        }
        catch (IdentityProviderUnavailableException)
        {
            return new LoginUnavailable();
        }
        catch (IdentityProviderResponseException)
        {
            return new LoginUpstreamError();
        }

        if (grant.StatusCode == StatusOk)
        {
            return grant.Token is null
                ? new LoginUpstreamError()
                : await VerifyAsync(grant.Token, ct);
        }

        // OpenEMR failing, as opposed to refusing the grant, is an upstream fault.
        if (grant.StatusCode >= 500 || grant.StatusCode == StatusRequestTimeout)
        {
            return new LoginUpstreamError();
        }

        return new LoginRejected();
    }

    /// <summary>
    /// Verifies the access token OpenEMR issued. A token that does not verify is an upstream fault,
    /// not a rejected grant.
    /// </summary>
    private async Task<LoginResult> VerifyAsync(OpenEmrToken token, CancellationToken ct)
    {
        VerifiedToken? verified;
        try
        {
            verified = await verifier.VerifyAsync(token.AccessToken, ct);
        }
        catch (TokenVerifierUnavailableException)
        {
            return new LoginUnavailable();
        }

        return verified is null
            ? new LoginUpstreamError()
            : new LoginSucceeded(token);
    }
}
