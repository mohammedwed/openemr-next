using OpenEmrNext.AuthFacade.Api.RateLimiting;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>Route conventions shared by endpoints that take credentials and issue tokens.</summary>
public static class PublicTokenEndpointExtensions
{
    /// <summary>
    /// Marks an endpoint as a public token endpoint. It is anonymous because it issues the token, and
    /// antiforgery is disabled because it accepts a form post from non-browser clients. Binding failures answer
    /// <c>invalid_request</c> (see <see cref="OAuthErrorBodies"/>), and requests are rate limited per client
    /// address, which slows password guessing.
    /// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder AsPublicTokenEndpoint<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithOAuthErrorBodies()
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.LoginPolicy);
}
