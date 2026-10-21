using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

public static class UserEndpoints
{
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
