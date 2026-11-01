using Microsoft.AspNetCore.Authentication;
using OpenIddict.Client.AspNetCore;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Carries the access/refresh token and <c>id_token</c> OpenIddict.Client already obtained during the code
/// exchange into a session sign-in's own properties, under the plain names
/// <see cref="AuthenticationTokenExtensions.GetTokenAsync(HttpContext, string, string)"/> expects. Without
/// this, the real OpenEMR token OpenIddict already holds is discarded the moment <c>/callback</c> returns,
/// and nothing that only has the session cookie — a browser-based client that was never handed a bearer
/// token (ADR 0026's token-handler/BFF discussion) — could ever reach OpenEMR's API, or sign out of it
/// (ADR 0040: OpenEMR's own <c>end_session_endpoint</c> requires the original <c>id_token</c> as
/// <c>id_token_hint</c>), on the user's behalf. OpenIddict's own AspNetCore integration stores these under
/// its own constant names (<see cref="OpenIddictClientAspNetCoreConstants.Tokens"/>), not the plain
/// ASP.NET Core convention, so they are read with one name and re-stored under the other.
/// </summary>
public static class OpenIddictTokenCapture
{
    /// <summary>Reads OpenEMR's access/refresh token, <c>id_token</c> and expiry off <paramref name="result"/>, if present.</summary>
    /// <param name="result">The <see cref="AuthenticateResult"/> OpenIddict's callback handler produced.</param>
    /// <returns>Properties carrying the tokens for the session sign-in, or none if OpenIddict captured none.</returns>
    public static AuthenticationProperties Capture(AuthenticateResult result)
    {
        var properties = new AuthenticationProperties();
        List<AuthenticationToken>? tokens = null;

        var accessToken = result.Properties?.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken);
        if (!string.IsNullOrEmpty(accessToken))
        {
            (tokens ??= []).Add(new AuthenticationToken { Name = "access_token", Value = accessToken });
        }

        var refreshToken = result.Properties?.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            (tokens ??= []).Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
        }

        var idToken = result.Properties?.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken);
        if (!string.IsNullOrEmpty(idToken))
        {
            (tokens ??= []).Add(new AuthenticationToken { Name = "id_token", Value = idToken });
        }

        var expiresAt = result.Properties?.GetTokenValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessTokenExpirationDate);
        if (!string.IsNullOrEmpty(expiresAt))
        {
            (tokens ??= []).Add(new AuthenticationToken { Name = "expires_at", Value = expiresAt });
        }

        if (tokens is not null)
        {
            properties.StoreTokens(tokens);
        }

        return properties;
    }
}
