using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>
/// Verifies OpenEMR-issued JWTs against the keys published at OpenEMR's JWKS endpoint. The keys
/// are cached in memory. A token whose signature does not verify triggers one refresh, subject
/// to a cooldown, so that key rotation is picked up without letting bad tokens force a fetch on every request.
/// </summary>
internal sealed class OpenEmrTokenVerifier(HttpClient http, IOptions<OpenEmrOptions> options) : IExternalTokenVerifier
{
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshCooldown = TimeSpan.FromMinutes(5);

    private readonly OpenEmrOptions _options = options.Value;
    private readonly Uri _jwksUri = new(options.Value.BaseUrl.TrimEnd('/') + "/oauth2/default/jwk");
    // Keeps claim names as the token wrote them, so "sub" is read as "sub" and not mapped to a URI.
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private JsonWebKeySet? _keys;
    private DateTime _fetchedAt = DateTime.MinValue;

    /// <inheritdoc/>
    public async Task<VerifiedToken?> VerifyAsync(string token, CancellationToken ct)
    {
        if (!_handler.CanReadToken(token))
        {
            return null;
        }

        var keys = await GetKeysAsync(forceRefresh: false, ct);
        var result = await ValidateAsync(token, keys);

        if (!result.IsValid && IsSignatureFailure(result.Exception))
        {
            keys = await GetKeysAsync(forceRefresh: true, ct);
            result = await ValidateAsync(token, keys);
        }

        if (!result.IsValid)
        {
            return null;
        }

        if (!result.Claims.TryGetValue("sub", out var subject) || subject is not string value || value.Length == 0)
        {
            return null;
        }

        return new VerifiedToken(value);
    }

    /// <summary>Checks every rule the token must satisfy: RS256 only, signature, issuer, audience and lifetime.</summary>
    private Task<TokenValidationResult> ValidateAsync(string token, JsonWebKeySet keys)
    {
        var parameters = new TokenValidationParameters
        {
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys.Keys,
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
        };

        return _handler.ValidateTokenAsync(token, parameters);
    }

    private static bool IsSignatureFailure(Exception? exception) =>
        exception is SecurityTokenSignatureKeyNotFoundException or SecurityTokenInvalidSignatureException;

    /// <summary>Returns the cached keys, fetching them when there are none, they have expired, or a refresh is forced outside the cooldown.</summary>
    private async Task<JsonWebKeySet> GetKeysAsync(bool forceRefresh, CancellationToken ct)
    {
        if (_keys is { } cached && !IsDue(forceRefresh))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Another request may have refreshed while this one waited for the lock.
            if (_keys is { } refreshed && !IsDue(forceRefresh))
            {
                return refreshed;
            }

            _keys = await FetchKeysAsync(ct);
            _fetchedAt = DateTime.UtcNow;
            return _keys;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool IsDue(bool forceRefresh)
    {
        var age = DateTime.UtcNow - _fetchedAt;
        if (forceRefresh)
        {
            return age >= RefreshCooldown;
        }

        return age >= KeyLifetime;
    }

    private async Task<JsonWebKeySet> FetchKeysAsync(CancellationToken ct)
    {
        try
        {
            var json = await http.GetStringAsync(_jwksUri, ct);
            return new JsonWebKeySet(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // A caller cancellation is not an outage, so it is not reported as one.
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            throw new TokenVerifierUnavailableException(ex);
        }
    }
}
