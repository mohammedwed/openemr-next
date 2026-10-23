using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Api.Contracts.Requests;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;
using OpenEmrNext.AuthFacade.Api.RateLimiting;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>The token endpoint, which forwards a password grant to OpenEMR and returns its token response.</summary>
public static class TokenEndpoints
{
    /// <summary>
    /// Maps <c>POST /connect/token</c>. The endpoint is anonymous because it issues the token,
    /// and antiforgery is disabled because it accepts a form post from non-browser clients. The endpoint is
    /// rate limited per client address, which slows password guessing.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", IssueTokenAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.LoginPolicy);

        return app;
    }

    /// <summary>
    /// Reads the form body and checks the grant type. Only the password grant is supported. OpenEMR
    /// verifies the credentials, and the token it issues is returned in AuthFacade's token response.
    /// </summary>
    /// <remarks>
    /// Responses: 200 with a <see cref="TokenResponse"/> on success; 400 <c>invalid_grant</c> when OpenEMR
    /// refuses the credentials; 400 for an unsupported grant type or invalid fields; 503 when OpenEMR is
    /// unreachable; 502 when OpenEMR fails, answers successfully without a usable token, or issues one
    /// that does not verify; 429 <c>too_many_requests</c> with <c>Retry-After</c> when the client address
    /// is over its limit.
    /// </remarks>
    private static async Task<IResult> IssueTokenAsync(HttpRequest request, PasswordLoginHandler login, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);

        if (!GrantType.TryParse(form[TokenRequestFields.GrantType].ToString(), out _))
            return Results.BadRequest(new ErrorResponse("unsupported_grant_type"));

        if (!Username.TryParse(form[TokenRequestFields.Username].ToString(), out var username) ||
            !Password.TryParse(form[TokenRequestFields.Password].ToString(), out var password))
            return Results.BadRequest(new ErrorResponse("invalid_request"));

        var result = await login.HandleAsync(username, password, ct);
        return ToHttpResult(result);
    }

    /// <summary>Maps a login outcome to the HTTP response the client receives.</summary>
    private static IResult ToHttpResult(LoginResult result) => result switch
    {
        LoginSucceeded success => Results.Json(
            new TokenResponse(success.Token.AccessToken, success.Token.TokenType, success.Token.ExpiresIn, success.Token.RefreshToken),
            statusCode: StatusCodes.Status200OK),
        LoginRejected => Results.Json(
            new ErrorResponse("invalid_grant", "The username or password is incorrect."),
            statusCode: StatusCodes.Status400BadRequest),
        LoginUnavailable => ServerError("OpenEMR is unreachable.", StatusCodes.Status503ServiceUnavailable),
        LoginUpstreamError => ServerError("OpenEMR returned an unexpected response.", StatusCodes.Status502BadGateway),
        _ => throw new InvalidOperationException("Unhandled login result.")
    };

    /// <summary>Builds an OAuth-style <c>server_error</c> response with the given description and status.</summary>
    private static IResult ServerError(string description, int statusCode) =>
        Results.Json(new ErrorResponse("server_error", description), statusCode: statusCode);
}
