using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>Endpoints that describe the caller, based on a validated service token.</summary>
public static class UserEndpoints
{
    /// <summary>
    /// Maps <c>GET /me</c>, which returns the subject and name claims of the bearer token.
    /// Requires authorization.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            Subject = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            Name = user.FindFirst(JwtRegisteredClaimNames.Name)?.Value
        })).RequireAuthorization();

        return app;
    }
}
