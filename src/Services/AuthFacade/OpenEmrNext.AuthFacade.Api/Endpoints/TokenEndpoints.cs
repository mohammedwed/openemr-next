using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>The token endpoint, which exchanges a password grant for this service's access token.</summary>
public static class TokenEndpoints
{
    /// <summary>
    /// Maps <c>POST /connect/token</c>. The endpoint is anonymous because it issues the token,
    /// and antiforgery is disabled because it accepts a form post from non-browser clients.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", IssueTokenAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        return app;
    }

    /// <summary>
    /// Reads the form body, validates it, and maps the login result to an HTTP response.
    /// </summary>
    /// <remarks>
    /// Responses: 200 with a Bearer token on success; 400 for an unsupported grant type or
    /// invalid fields; OpenEMR's own status and body on rejection; 503 when OpenEMR is
    /// unreachable; 502 when OpenEMR answers successfully without a usable token.
    /// </remarks>
    private static async Task<IResult> IssueTokenAsync(
        HttpRequest request, PasswordLoginHandler login, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);

        // The only supported grant is "password", so the parsed value is not needed here.
        if (!GrantType.TryParse(form["grant_type"].ToString(), out _))
            return Results.BadRequest(new { error = "unsupported_grant_type" });

        if (!Username.TryParse(form["username"].ToString(), out var username) ||
            !Password.TryParse(form["password"].ToString(), out var password))
            return Results.BadRequest(new { error = "invalid_request" });

        var result = await login.HandleAsync(username, password, ct);
        return ToHttpResult(result);
    }

    /// <summary>Maps a login outcome to the HTTP response the client receives.</summary>
    private static IResult ToHttpResult(LoginResult result) => result switch
    {
        LoginSucceeded success => Results.Ok(new
        {
            access_token = success.Token.AccessToken,
            token_type = "Bearer",
            expires_in = success.Token.ExpiresInSeconds
        }),
        LoginRejected rejected => Results.Content(rejected.Body, "application/json", statusCode: rejected.StatusCode),
        LoginUnavailable => ServerError("OpenEMR is unreachable.", StatusCodes.Status503ServiceUnavailable),
        LoginUpstreamError => ServerError("OpenEMR returned an unexpected response.", StatusCodes.Status502BadGateway),
        _ => throw new InvalidOperationException("Unhandled login result.")
    };

    /// <summary>Builds an OAuth-style <c>server_error</c> response with the given description and status.</summary>
    private static IResult ServerError(string description, int statusCode) =>
        Results.Json(new { error = "server_error", error_description = description }, statusCode: statusCode);
}
