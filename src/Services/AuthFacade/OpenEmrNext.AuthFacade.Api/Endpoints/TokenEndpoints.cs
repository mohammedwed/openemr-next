using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Api.Contracts.Requests;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;
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

    /// <summary>The token type a subject token must have for the token exchange grant.</summary>
    private const string JwtTokenType = "urn:ietf:params:oauth:token-type:jwt";

    /// <summary>The token type this service issues in a token exchange response.</summary>
    private const string AccessTokenType = "urn:ietf:params:oauth:token-type:access_token";

    /// <summary>
    /// Reads the form body and dispatches on the grant type. Password logins are verified by OpenEMR;
    /// token exchanges verify an OpenEMR-issued JWT locally.
    /// </summary>
    /// <remarks>
    /// Responses: 200 with a Bearer token on success; 400 for an unsupported grant type, invalid
    /// fields, or a subject token that does not verify; OpenEMR's own status and body on a rejected
    /// password login; 503 when OpenEMR or its keys are unreachable; 502 when OpenEMR answers
    /// successfully without a usable token.
    /// </remarks>
    private static async Task<IResult> IssueTokenAsync(
        HttpRequest request, PasswordLoginHandler login, TokenExchangeHandler exchange, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);

        if (!GrantType.TryParse(form[TokenRequestFields.GrantType].ToString(), out var grantType))
            return Results.BadRequest(new ErrorResponse("unsupported_grant_type"));

        return grantType.Value == GrantType.TokenExchange
            ? await ExchangeAsync(form, exchange, ct)
            : await PasswordLoginAsync(form, login, ct);
    }

    /// <summary>Validates the username and password fields, then runs the password login.</summary>
    private static async Task<IResult> PasswordLoginAsync(IFormCollection form, PasswordLoginHandler login, CancellationToken ct)
    {
        if (!Username.TryParse(form[TokenRequestFields.Username].ToString(), out var username) ||
            !Password.TryParse(form[TokenRequestFields.Password].ToString(), out var password))
            return Results.BadRequest(new ErrorResponse("invalid_request"));

        var result = await login.HandleAsync(username, password, ct);
        return ToHttpResult(result);
    }

    /// <summary>Validates the subject token fields, then runs the RFC 8693 exchange.</summary>
    private static async Task<IResult> ExchangeAsync(IFormCollection form, TokenExchangeHandler exchange, CancellationToken ct)
    {
        var subjectToken = form[TokenRequestFields.SubjectToken].ToString();
        var subjectTokenType = form[TokenRequestFields.SubjectTokenType].ToString();

        if (subjectToken.Length == 0 || subjectTokenType != JwtTokenType)
            return Results.BadRequest(new ErrorResponse("invalid_request"));

        var result = await exchange.HandleAsync(subjectToken, ct);
        return ToExchangeResult(result);
    }

    /// <summary>Maps a token exchange outcome to the HTTP response the client receives.</summary>
    private static IResult ToExchangeResult(TokenExchangeResult result) => result switch
    {
        TokenExchangeSucceeded success => Results.Ok(new TokenExchangeResponse(
            success.Token.AccessToken, AccessTokenType, "Bearer", success.Token.ExpiresInSeconds)),
        TokenExchangeRejected => Results.BadRequest(new ErrorResponse("invalid_grant", "The subject token is not valid.")),
        TokenExchangeUnavailable => ServerError("The token issuer's keys are unavailable.", StatusCodes.Status503ServiceUnavailable),
        _ => throw new InvalidOperationException("Unhandled token exchange result.")
    };

    /// <summary>Maps a login outcome to the HTTP response the client receives.</summary>
    private static IResult ToHttpResult(LoginResult result) => result switch
    {
        LoginSucceeded success => Results.Ok(new TokenResponse(
            success.Token.AccessToken, "Bearer", success.Token.ExpiresInSeconds)),
        LoginRejected rejected => Results.Content(rejected.Body, "application/json", statusCode: rejected.StatusCode),
        LoginUnavailable => ServerError("OpenEMR is unreachable.", StatusCodes.Status503ServiceUnavailable),
        LoginUpstreamError => ServerError("OpenEMR returned an unexpected response.", StatusCodes.Status502BadGateway),
        _ => throw new InvalidOperationException("Unhandled login result.")
    };

    /// <summary>Builds an OAuth-style <c>server_error</c> response with the given description and status.</summary>
    private static IResult ServerError(string description, int statusCode) =>
        Results.Json(new ErrorResponse("server_error", description), statusCode: statusCode);
}
