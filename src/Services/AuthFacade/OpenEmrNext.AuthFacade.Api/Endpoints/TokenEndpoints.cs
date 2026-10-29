using System.ComponentModel.DataAnnotations;
using OpenEmrNext.AuthFacade.Api.Contracts.Requests;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;
using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>The token endpoint, which forwards a password or refresh grant to OpenEMR and returns its token response.</summary>
public static class TokenEndpoints
{
    /// <summary>
    /// Maps <c>POST /api/v1/connect/token</c>, which verifies a password or refresh grant with OpenEMR and
    /// returns the token OpenEMR issues in AuthFacade's token response. The endpoint is named <c>IssueToken</c>,
    /// which becomes its OpenAPI <c>operationId</c> and the method name in generated clients.
    /// </summary>
    /// <remarks>
    /// The form fields bind to <see cref="TokenRequest"/>, whose properties carry their validation rules, including
    /// which fields are required for each grant type (see <see cref="TokenRequest.Validate"/>).
    /// <see cref="RequestValidationExtensions.WithRequestValidation{TRequest}"/> checks them before the handler runs. The route conventions come from <see cref="PublicTokenEndpointExtensions.AsPublicTokenEndpoint"/>.
    /// <para>Responses:</para>
    /// <list type="bullet">
    /// <item>200 with a <see cref="TokenResponse"/> on success.</item>
    /// <item>400 <c>invalid_grant</c> when OpenEMR refuses the credentials or refresh token.</item>
    /// <item>400 <c>invalid_request</c> when a field is missing or invalid, including an unsupported grant type.</item>
    /// <item>415 with no body when the request is not a form.</item>
    /// <item>429 <c>too_many_requests</c> with <c>Retry-After</c> when the client address is over its limit.</item>
    /// <item>502 when OpenEMR fails, answers successfully without a usable token, or issues one that does not verify.</item>
    /// <item>503 when OpenEMR is unreachable.</item>
    /// </list>
    /// </remarks>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", async (
            [AsParameters] TokenRequest request,
            TokenGrantHandler login,
            CancellationToken ct) =>
                    {
                        var result = request.GrantType switch
                        {
                            GrantType.Password =>
                                await login.HandleAsync(new Username(request.Username!), new Password(request.Password!), ct),
                            GrantType.RefreshToken =>
                                await login.HandleAsync(new RefreshToken(request.RefreshToken!), ct),
                            _ => throw new InvalidOperationException(
                                $"Unhandled grant type: {request.GrantType}. {nameof(TokenRequest.GrantType)}'s " +
                                $"{nameof(AllowedValuesAttribute)} should have rejected this before the handler ran.")
                        };
                        return ToHttpResult(result);
                    })
            .WithRequestValidation<TokenRequest>()
            .WithName("IssueToken")
            .WithTags("Authentication")
            .WithSummary("Log in with an OpenEMR username and password, or redeem a refresh token")
            .WithDescription(
                "Verifies a password or refresh grant with OpenEMR and returns the access token OpenEMR issues. " +
                "400 is invalid_grant when OpenEMR refuses the grant and invalid_request when a form field " +
                "is missing or invalid, including an unsupported grant type.")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status415UnsupportedMediaType)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .AsPublicTokenEndpoint();

        return app;
    }

    /// <summary>Maps a login outcome to the HTTP response the client receives.</summary>
    private static IResult ToHttpResult(LoginResult result) => result switch
    {
        LoginSucceeded success => Results.Ok(TokenResponse.From(success.Token)),
        LoginRejected => Results.BadRequest(ErrorResponse.InvalidGrant()),
        LoginUnavailable => Results.Json(
            ErrorResponse.ServerError("OpenEMR is unreachable."),
            statusCode: StatusCodes.Status503ServiceUnavailable),
        LoginUpstreamError => Results.Json(
            ErrorResponse.ServerError("OpenEMR returned an unexpected response."),
            statusCode: StatusCodes.Status502BadGateway),
        _ => throw new InvalidOperationException("Unhandled login result.")
    };
}
