using OpenEmrNext.AuthFacade.Application.Ports;
using Polly;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>
/// Wraps an <see cref="IExternalTokenVerifier"/> in a circuit breaker for the case where OpenEMR's keys
/// cannot be fetched. While the circuit is open, verification fails with
/// <see cref="TokenVerifierUnavailableException"/> and the JWKS endpoint is not called.
/// </summary>
/// <remarks>
/// Only an unreachable key set counts as a failure. A token that does not verify, such as a bad signature
/// or an expired token, returns null from the inner verifier and does not count, because that says nothing
/// about OpenEMR's health.
/// </remarks>
internal sealed class CircuitBreakingTokenVerifier(
    IExternalTokenVerifier inner,
    ResiliencePipeline<VerifiedToken?> pipeline) : IExternalTokenVerifier
{
    /// <inheritdoc/>
    public async Task<VerifiedToken?> VerifyAsync(string token, CancellationToken ct)
    {
        try
        {
            return await pipeline.ExecuteAsync(
                async callToken => await inner.VerifyAsync(token, callToken), ct);
        }
        catch (BrokenCircuitException ex)
        {
            // The circuit is open, so the key set was not fetched.
            throw new TokenVerifierUnavailableException(ex);
        }
    }

    /// <summary>Builds the breaker with the thresholds in <paramref name="options"/>.</summary>
    /// <param name="options">The thresholds to use.</param>
    /// <returns>A pipeline to share across requests.</returns>
    internal static ResiliencePipeline<VerifiedToken?> CreatePipeline(CircuitBreakerOptions options) =>
        CircuitBreakerPipeline.Create(options, new PredicateBuilder<VerifiedToken?>()
            .Handle<TokenVerifierUnavailableException>());
}
