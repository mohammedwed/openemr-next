using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

public sealed class PasswordLoginHandler(IOpenEmrIdentityProvider identityProvider, IAccessTokenIssuer tokenIssuer)
{
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

        if (grant.Subject is null)
        {
            return new LoginRejected(grant.StatusCode, grant.Body);
        }

        // OpenEMR accepted the login. Issue this service's own short-lived token;
        // OpenEMR's token is not returned to the caller.
        return new LoginSucceeded(tokenIssuer.Issue(grant.Subject, username.Value));
    }
}
