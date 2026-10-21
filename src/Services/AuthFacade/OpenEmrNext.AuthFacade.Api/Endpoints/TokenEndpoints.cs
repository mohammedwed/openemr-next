using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", IssueTokenAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> IssueTokenAsync(
        HttpRequest request, PasswordLoginHandler login, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);

        if (!GrantType.TryParse(form["grant_type"].ToString(), out _))
            return Results.BadRequest(new { error = "unsupported_grant_type" });

        if (!Username.TryParse(form["username"].ToString(), out var username) ||
            !Password.TryParse(form["password"].ToString(), out var password))
            return Results.BadRequest(new { error = "invalid_request" });

        var result = await login.HandleAsync(username, password, ct);

        return result switch
        {
            LoginSucceeded success => Results.Ok(new
            {
                access_token = success.Token.AccessToken,
                token_type = "Bearer",
                expires_in = success.Token.ExpiresInSeconds
            }),
            LoginRejected rejected => Results.Content(rejected.Body, "application/json", statusCode: rejected.StatusCode),
            LoginUnavailable => Results.Json(new
            {
                error = "server_error",
                error_description = "OpenEMR is unreachable."
            }, statusCode: StatusCodes.Status503ServiceUnavailable),
            LoginUpstreamError => Results.Json(new
            {
                error = "server_error",
                error_description = "OpenEMR returned an unexpected response."
            }, statusCode: StatusCodes.Status502BadGateway),
            _ => throw new InvalidOperationException("Unhandled login result.")
        };
    }
}
