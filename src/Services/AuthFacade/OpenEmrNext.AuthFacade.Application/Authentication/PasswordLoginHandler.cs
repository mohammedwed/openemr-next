using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

/// <summary>
/// Handles a password login: verifies the credentials with OpenEMR and, on success, issues
/// this service's own access token.
/// </summary>
public sealed class PasswordLoginHandler(IOpenEmrIdentityProvider identityProvider, IAccessTokenIssuer tokenIssuer)
{
    /// <summary>Attempts a password login.</summary>
    /// <param name="username">The validated login name.</param>
    /// <param name="password">The validated password.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>
    /// <see cref="LoginSucceeded"/> when OpenEMR accepts the credentials,
    /// <see cref="LoginRejected"/> when it refuses them, <see cref="LoginUnavailable"/> when it
    /// cannot be reached, or <see cref="LoginUpstreamError"/> when its success response is unusable.
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

        // A subject is present only when OpenEMR accepted the credentials.
        if (grant.Subject is not { } subject)
        {
            return new LoginRejected(grant.StatusCode, grant.Body);
        }

        // OpenEMR accepted the login. Issue this service's own short-lived token;
        // OpenEMR's token is not returned to the caller.
        return new LoginSucceeded(tokenIssuer.Issue(subject, username.Value));
    }
}
