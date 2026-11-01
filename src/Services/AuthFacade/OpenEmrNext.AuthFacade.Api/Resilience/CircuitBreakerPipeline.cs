using Polly;

namespace OpenEmrNext.AuthFacade.Api.Resilience;

/// <summary>Builds the circuit breaker pipelines shared by the call sites that guard OpenEMR calls (ADR 0041).</summary>
internal static class CircuitBreakerPipeline
{
    /// <summary>Builds a circuit breaker with the thresholds from <paramref name="options"/>. Retries are never added.</summary>
    /// <typeparam name="T">The result type of the guarded call.</typeparam>
    /// <param name="options">The thresholds to use.</param>
    /// <param name="failures">Which exceptions and results count as failures.</param>
    /// <returns>A pipeline to share across requests, so that its state spans them.</returns>
    internal static ResiliencePipeline<T> Create<T>(CircuitBreakerOptions options, PredicateBuilder<T> failures) =>
        new ResiliencePipelineBuilder<T>()
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<T>
            {
                FailureRatio = options.FailureRatio,
                SamplingDuration = options.SamplingDuration,
                MinimumThroughput = options.MinimumThroughput,
                BreakDuration = options.BreakDuration,
                ShouldHandle = failures
            })
            .Build();
}
