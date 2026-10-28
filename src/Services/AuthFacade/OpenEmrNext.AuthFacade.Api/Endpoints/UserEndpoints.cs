using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>Endpoints that describe the caller, based on a validated service token.</summary>
public static class UserEndpoints
{
    /// <summary>
    /// Maps <c>GET /api/v1/me</c>, which returns the subject and name claims of the bearer token.
    /// Requires authorization. The endpoint is named <c>GetCurrentUser</c>, which becomes its OpenAPI
    /// <c>operationId</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new MeResponse(
            user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            user.FindFirst(JwtRegisteredClaimNames.Name)?.Value)))
            .WithName("GetCurrentUser")
            .WithTags("Identity")
            .WithSummary("Get the subject and name of the bearer token")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        return app;
    }
}
