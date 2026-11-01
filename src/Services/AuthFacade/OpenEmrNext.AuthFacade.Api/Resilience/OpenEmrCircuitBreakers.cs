using Polly;

namespace OpenEmrNext.AuthFacade.Api.Resilience;

/// <summary>
/// The failure predicates for each of AuthFacade's three remaining calls to OpenEMR (ADR 0041): the token
/// exchange during <c>/callback</c>, the JWKS fetch behind it, and the BFF proxy's forwarded API calls. A
/// refused or rejected request (a 4xx) never counts as a failure in any of them — a wrong code, an expired
/// token, or a resource the caller cannot access says nothing about whether OpenEMR itself is up.
/// </summary>
internal static class OpenEmrCircuitBreakers
{
    /// <summary>
    /// For a call that returns an <see cref="HttpResponseMessage"/>: the token exchange and the BFF proxy's
    /// forwarded calls both shape their failures this way, but each gets its own pipeline instance
    /// (separate breaker state) since they guard different OpenEMR endpoints.
    /// </summary>
    internal static ResiliencePipeline<HttpResponseMessage> CreateHttpPipeline(CircuitBreakerOptions options) =>
        CircuitBreakerPipeline.Create(options, new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>()
            .HandleResult(response => (int)response.StatusCode == 408 || (int)response.StatusCode >= 500));

    /// <summary>For <see cref="OpenIddictClient.OpenEmrConfigurationManager"/>'s <c>HttpClient.GetStringAsync</c> JWKS fetch.</summary>
    internal static ResiliencePipeline<string> CreateJwksPipeline(CircuitBreakerOptions options) =>
        CircuitBreakerPipeline.Create(options, new PredicateBuilder<string>()
            .Handle<HttpRequestException>()
            .Handle<TaskCanceledException>());
}
