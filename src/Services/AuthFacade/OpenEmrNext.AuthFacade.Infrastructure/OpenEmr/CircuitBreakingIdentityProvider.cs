using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using Polly;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>
/// Wraps an <see cref="IIdentityProvider"/> in a circuit breaker. While the circuit is open, password
/// grants fail immediately with <see cref="IdentityProviderUnavailableException"/> and OpenEMR is not called.
/// </summary>
/// <remarks>
/// The pipeline holds the breaker state, so it must be shared across requests. The wrapped provider does
/// not need to be. Failures are unreachable OpenEMR, timeouts, unusable success responses, and 408 or 5xx
/// statuses. A refused login (a 4xx) does not count, because a wrong password is not an outage.
/// </remarks>
internal sealed class CircuitBreakingIdentityProvider(
    IIdentityProvider inner,
    ResiliencePipeline<PasswordGrantResult> pipeline) : IIdentityProvider
{
    /// <inheritdoc/>
    public async Task<PasswordGrantResult> PasswordGrantAsync(
        Username username, Password password, CancellationToken ct)
    {
        try
        {
            return await pipeline.ExecuteAsync(
                async token => await inner.PasswordGrantAsync(username, password, token), ct);
        }
        catch (BrokenCircuitException ex)
        {
            // The circuit is open, so no request was sent to OpenEMR.
            throw new IdentityProviderUnavailableException(ex);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Shares the password grant's breaker state: both call the same OpenEMR token endpoint.</remarks>
    public async Task<PasswordGrantResult> RefreshGrantAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        try
        {
            return await pipeline.ExecuteAsync(
                async token => await inner.RefreshGrantAsync(refreshToken, token), ct);
        }
        catch (BrokenCircuitException ex)
        {
            // The circuit is open, so no request was sent to OpenEMR.
            throw new IdentityProviderUnavailableException(ex);
        }
    }

    /// <summary>Builds the breaker with the thresholds in <paramref name="options"/>.</summary>
    /// <param name="options">The thresholds to use.</param>
    /// <returns>A pipeline to share across requests.</returns>
    internal static ResiliencePipeline<PasswordGrantResult> CreatePipeline(CircuitBreakerOptions options) =>
        CircuitBreakerPipeline.Create(options, new PredicateBuilder<PasswordGrantResult>()
            .Handle<IdentityProviderUnavailableException>()
            .Handle<IdentityProviderResponseException>()
            .HandleResult(result => result.StatusCode == 408 || result.StatusCode >= 500));
}
