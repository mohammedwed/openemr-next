using Microsoft.AspNetCore.Authorization;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// Maps the reverse proxy registered by <c>AddOpenEmrBffProxy</c>. Requires the <c>AuthFacadeSession</c>
/// cookie specifically, not the alternative <c>Bearer</c> scheme <c>/me</c> also accepts: a caller that
/// already holds a bearer token has no need for this proxy and should call OpenEMR directly.
/// </summary>
public static class OpenEmrBffProxyEndpoints
{
    /// <summary>Maps every path under <c>/bff/openemr</c>.</summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapOpenEmrBffProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapReverseProxy()
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(OpenIddictClientServiceCollectionExtensions.SessionScheme)
                .RequireAuthenticatedUser())
            .ExcludeFromDescription();

        return app;
    }
}
