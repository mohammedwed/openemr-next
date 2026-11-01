using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Polly;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Supplies OpenIddict.Client with a configuration whose endpoints are fixed (ADR 0024's internal/public
/// split — see <c>OpenIddictClientServiceCollectionExtensions.BuildRegistration</c>), but whose JWKS is
/// fetched and cached, not static. Setting <see cref="OpenIddictClientRegistration.Configuration"/> alone
/// makes OpenIddict wrap it in a <c>StaticConfigurationManager</c>, which returns that exact object forever
/// — <c>JsonWebKeySetUri</c> is only ever followed as part of a live discovery-document fetch, which never
/// happens here, so the <c>id_token</c>'s signing key was never found (confirmed: that was ID2090's first
/// cause). Populating <see cref="OpenIddictConfiguration.JsonWebKeySet"/> is still not enough on its own:
/// token validation (<c>OpenIddictClientHandlers.Protection</c>) reads <c>Configuration.SigningKeys</c>, a
/// flat list, not the key set — OpenIddict's own discovery pipeline copies one into the other
/// (<c>OpenIddictClientRetriever</c>'s "Copy the signing keys found in the JSON Web Key Set to the
/// SigningKeys collection"), but that pipeline never runs here either, so this does the same copy itself
/// (confirmed: that was ID2090's second cause, after fixing the first). Mirrors <c>OpenEmrTokenVerifier</c>'s
/// own cache-with-refresh pattern for the same JWKS endpoint.
/// <para>
/// <see cref="GetConfigurationAsync"/> is also what the challenge step resolves to read the (already fully
/// known, static) authorize endpoint from — confirmed by a unit test that fails without this: OpenIddict
/// calls it there too, not only before validating a token. A JWKS fetch failure must not fail that call, or
/// a temporarily unreachable JWKS endpoint would break the login redirect itself, which never needs JWKS at
/// all. A failed fetch is swallowed here and simply retried on the next call; if the keys are still missing
/// once token validation actually needs them, OpenIddict reports that on its own (<c>ID2090</c>).
/// </para>
/// <para>
/// The fetch runs through <paramref name="pipeline"/>, a circuit breaker (ADR 0041): once OpenEMR's JWKS
/// endpoint has failed enough times, further calls fail immediately with <see cref="BrokenCircuitException"/>
/// instead of waiting out a real timeout against an endpoint already known to be down. That exception is
/// swallowed the same way the others are — the cached keys are kept and the next call retries once the
/// breaker's own cooldown lets a trial call through.
/// </para>
/// </summary>
internal sealed class OpenEmrConfigurationManager(
    HttpClient http, OpenIddictConfiguration configuration, Uri jwksUri, ResiliencePipeline<string> pipeline)
    : IConfigurationManager<OpenIddictConfiguration>
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTime _fetchedAt = DateTime.MinValue;
    private volatile bool _refreshRequested;

    /// <inheritdoc/>
    public async Task<OpenIddictConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        if (!IsDue())
        {
            return configuration;
        }

        await _refreshLock.WaitAsync(cancel);
        try
        {
            if (!IsDue())
            {
                return configuration;
            }

            try
            {
                var json = await pipeline.ExecuteAsync(
                    async token => await http.GetStringAsync(jwksUri, token), cancel);
                var keySet = new JsonWebKeySet(json);

                configuration.JsonWebKeySet = keySet;
                configuration.SigningKeys.Clear();
                foreach (var key in keySet.GetSigningKeys())
                {
                    configuration.SigningKeys.Add(key);
                }

                _fetchedAt = DateTime.UtcNow;
                _refreshRequested = false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or BrokenCircuitException)
            {
                // Leave _fetchedAt and the cached keys as they were: the next call retries. Letting this
                // propagate would fail the challenge step too, not just token validation (see remarks).
                if (cancel.IsCancellationRequested)
                {
                    throw;
                }
            }

            return configuration;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc/>
    public void RequestRefresh() => _refreshRequested = true;

    private bool IsDue() => _refreshRequested || DateTime.UtcNow - _fetchedAt >= CacheLifetime;
}
