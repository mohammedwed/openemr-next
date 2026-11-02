using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenEmrNext.AuthFacade.Composition;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Silently redeems the session's refresh token when its access token is at or near expiry, so the BFF
/// proxy's forwarded calls (ADR 0029) keep working without the browser ever noticing (ADR 0045) — the gap
/// ADR 0029 explicitly deferred. Only the BFF proxy needs this: <c>/me</c> never calls OpenEMR, so there is
/// nothing for it to refresh against.
/// </summary>
internal sealed class AccessTokenRefresher(OpenIddictClientService clientService, IOptions<OpenEmrOptions> openEmrOptions)
{
    // A real request to OpenEMR takes a moment to actually arrive after this check runs; a token only
    // seconds away from expiring should already be treated as expired, or the call it is about to
    // authorize could still land past its exact expiry.
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns a still-valid access token for the session <paramref name="context"/> authenticated against,
    /// refreshing it first if the one on record is expired or about to be. Returns the original access
    /// token unchanged when there is nothing to refresh with, or when the refresh attempt itself failed —
    /// OpenEMR answers 401 on a stale token itself, the same graceful-degradation path a session with no
    /// token at all already takes; this proxy makes no auth decision of its own (ADR 0013).
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var authenticateResult = await context.AuthenticateAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme);
        var properties = authenticateResult.Properties;
        if (!authenticateResult.Succeeded || properties is null)
        {
            return null;
        }

        var accessToken = properties.GetTokenValue("access_token");
        if (string.IsNullOrEmpty(accessToken) || !IsExpiredOrNearExpiry(properties.GetTokenValue("expires_at")))
        {
            return accessToken;
        }

        var refreshToken = properties.GetTokenValue("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            // An older session than the refresh grant AuthFacade now requests, or one OpenEMR never issued
            // a refresh token for -- nothing to redeem; fall back to the stale token, same as above.
            return accessToken;
        }

        try
        {
            var result = await clientService.AuthenticateWithRefreshTokenAsync(new OpenIddictClientModels.RefreshTokenAuthenticationRequest
            {
                Issuer = OpenIddictClientServiceCollectionExtensions.NormalizeIssuer(openEmrOptions.Value),
                RefreshToken = refreshToken,
                CancellationToken = cancellationToken
            });

            OpenIddictTokenCapture.ApplyRefresh(properties, result);
            await context.SignInAsync(OpenIddictClientServiceCollectionExtensions.SessionScheme, authenticateResult.Principal, properties);
            return result.AccessToken;
        }
        catch (Exception exception) when (exception is BrokenCircuitException or OpenIddictExceptions.ProtocolException)
        {
            // The refresh token itself was rejected (expired, revoked, or simply not recognized), or the
            // circuit guarding OpenEMR's token endpoint (ADR 0041, shared with the original code exchange)
            // is open. Either way this is OpenEMR's call to make, not this proxy's.
            return accessToken;
        }
    }

    private static bool IsExpiredOrNearExpiry(string? expiresAtRaw)
    {
        if (string.IsNullOrEmpty(expiresAtRaw) ||
            !DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
        {
            // No expiry on record to compare against -- never refresh speculatively; let OpenEMR's own
            // check of the access token decide instead.
            return false;
        }

        return expiresAt <= DateTimeOffset.UtcNow + ExpirySkew;
    }
}
