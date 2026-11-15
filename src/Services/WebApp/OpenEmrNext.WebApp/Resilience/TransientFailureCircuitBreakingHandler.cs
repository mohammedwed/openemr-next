using Polly;
using Polly.CircuitBreaker;

namespace OpenEmrNext.WebApp.Resilience;

/// <summary>
/// Guards one of WebApp's dependencies (AuthFacade, via the generated <c>AuthFacadeClient</c>, or
/// PatientManagement, via <c>PatientManagementClient</c>) with a circuit breaker, mirroring
/// AuthFacade.Api's own breakers that guard its calls to OpenEMR (ADR 0041). Each dependency gets its
/// own <c>HttpClient</c> and its own instance of this handler, so a down PatientManagement trips only
/// its own breaker, never AuthFacade's. Once a dependency has failed enough times, further calls to it
/// fail immediately with a synthetic 503 instead of each one waiting out its own timeout against a
/// dependency already known to be down. A 4xx response (for example a 401 from an expired session) is
/// never a failure here, same as ADR 0041's own breakers -- only a connection problem, a timeout, or a
/// 5xx/408 says anything about whether the dependency itself is up. One fixed threshold set, not a
/// configurable <c>CircuitBreakerOptions</c> like AuthFacade.Api's: WebApp has no configuration/
/// options-binding infrastructure today.
/// </summary>
internal sealed class TransientFailureCircuitBreakingHandler : DelegatingHandler
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
            // No request was sent to the dependency. The generated client surfaces this the same way
            // it surfaces a real 503 from the dependency itself -- an ApiException callers already
            // have to handle.
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }
    }
}
