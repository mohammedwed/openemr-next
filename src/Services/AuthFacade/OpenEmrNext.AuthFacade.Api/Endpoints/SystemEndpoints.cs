namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>Unauthenticated health and smoke-test endpoints.</summary>
public static class SystemEndpoints
{
    /// <summary>Maps <c>GET /hello</c>, which returns a fixed greeting and requires no token.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/hello", () => Results.Ok(new { message = "Hello from AuthFacade" }))
            .AllowAnonymous();
        return app;
    }
}
