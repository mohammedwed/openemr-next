namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>Maps every AuthFacade endpoint group in one place.</summary>
public static class AuthFacadeEndpoints
{
    /// <summary>Maps the system, token and user endpoint groups.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAuthFacadeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapSystemEndpoints();
        app.MapTokenEndpoints();
        app.MapUserEndpoints();
        return app;
    }
}
