using OpenEmrNext.AuthFacade.Api.Endpoints;

namespace OpenEmrNext.AuthFacade.Api;

/// <summary>Maps every AuthFacade endpoint group in one place.</summary>
public static class AuthFacadeEndpoints
{
    /// <summary>
    /// Maps the system, token and user endpoint groups under the <c>/api/v1</c> root group, so every
    /// route carries its API version. A later version gets its own root group beside this one.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAuthFacadeEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness sits outside the versioned group, so probes do not depend on the API version.
        app.MapSystemEndpoints();

        var v1 = app.MapGroup("/api/v1");
        v1.MapTokenEndpoints();
        v1.MapUserEndpoints();
        return app;
    }
}
