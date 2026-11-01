using Polly;
using Polly.CircuitBreaker;
using Yarp.ReverseProxy.Forwarder;

namespace OpenEmrNext.AuthFacade.Api.Proxy;

/// <summary>
/// Guards the BFF proxy's forwarded calls to OpenEMR with a circuit breaker (ADR 0041): once OpenEMR's API
/// has failed enough times, further requests fail immediately with a synthetic 503 instead of each one
/// waiting out its own timeout against an endpoint already known to be down. OpenEMR's own 4xx responses
/// (a rejected or not-found resource) are relayed through unchanged, same as always — only a connection
/// problem or a 5xx/408 counts as a failure here.
/// </summary>
/// <remarks>
/// <see cref="WrapHandler"/> is <see cref="ForwarderHttpClientFactory"/>'s own documented extension point
/// for inserting a <see cref="DelegatingHandler"/> into the forwarded request's handler chain. Registered
/// with a plain <c>AddSingleton</c> after <c>AddReverseProxy()</c>'s own <c>TryAddSingleton</c> for the
/// same service type, so this implementation is the one actually resolved (last registration wins).
/// </remarks>
internal sealed class CircuitBreakingForwarderHttpClientFactory(ResiliencePipeline<HttpResponseMessage> pipeline)
    : ForwarderHttpClientFactory
{
    protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler) =>
        new CircuitBreakingHandler(pipeline) { InnerHandler = base.WrapHandler(context, handler) };

    private sealed class CircuitBreakingHandler(ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await pipeline.ExecuteAsync(
                    async token => await base.SendAsync(request, token), cancellationToken);
            }
            catch (BrokenCircuitException)
            {
                // No request was sent to OpenEMR. YARP relays this response unchanged (ADR 0013's
                // passthrough), so the browser sees a plain 503 rather than this handler's exception.
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            }
        }
    }
}
