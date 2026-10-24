using OpenEmrNext.AuthFacade.Api.Contracts.Responses;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>Unauthenticated health and smoke-test endpoints.</summary>
public static class SystemEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/health</c>, which returns a fixed greeting and requires no token. It sits outside the
    /// <c>/api/v1</c> group and is left out of the OpenAPI document, since it is for probes, not clients.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new HelloResponse("Hello from AuthFacade")))
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }
}
