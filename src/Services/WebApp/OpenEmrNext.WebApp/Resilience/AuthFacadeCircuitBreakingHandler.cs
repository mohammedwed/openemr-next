using Polly;
using Polly.CircuitBreaker;

namespace OpenEmrNext.WebApp.Resilience;

/// <summary>
/// Guards WebApp's calls to AuthFacade (through the generated <c>AuthFacadeClient</c>) with a circuit
/// breaker, mirroring AuthFacade.Api's own breakers that guard its calls to OpenEMR (ADR 0041) -- this is
/// the one remaining hop neither of those cover. Once AuthFacade has failed enough times, further calls
/// fail immediately with a synthetic 503 instead of each one waiting out its own timeout against a
/// dependency already known to be down. A 4xx from AuthFacade (for example a 401 from an expired session)
/// is never a failure here, same as ADR 0041's own breakers -- only a connection problem, a timeout, or a
/// 5xx/408 says anything about whether AuthFacade itself is up. One fixed threshold set, not a configurable
/// <c>CircuitBreakerOptions</c> like AuthFacade.Api's: WebApp has no configuration/options-binding
/// infrastructure today, and there is exactly one dependency to guard here, not three independent ones.
/// </summary>
internal sealed class AuthFacadeCircuitBreakingHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(response => (int)response.StatusCode == 408 || (int)response.StatusCode >= 500)
        })
        .Build();

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await base.SendAsync(request, token), cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            // No request was sent to AuthFacade. The generated client surfaces this the same way it
            // surfaces a real 503 from AuthFacade -- an ApiException callers already have to handle.
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }
}
