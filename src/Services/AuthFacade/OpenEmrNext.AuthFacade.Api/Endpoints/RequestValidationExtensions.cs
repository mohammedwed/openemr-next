using System.ComponentModel.DataAnnotations;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// Validates an endpoint's request DTO against its data annotations before the handler runs. A failure answers
/// <c>invalid_request</c>, so the handler receives only requests whose properties pass their attributes.
/// </summary>
public static class RequestValidationExtensions
{
    /// <summary>Checks every property of <paramref name="request"/> against its validation attributes.</summary>
    /// <param name="request">The request DTO to check.</param>
    /// <returns>True when all properties pass.</returns>
    public static bool IsValid(object request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults: null, validateAllProperties: true);

    /// <summary>
    /// Adds a filter that validates the endpoint's <typeparamref name="TRequest"/> argument with its data annotations.
    /// The endpoint must take that type as one of its parameters, for example through <c>[AsParameters]</c>.
    /// </summary>
    /// <typeparam name="TRequest">The request DTO type to validate.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The endpoint has no argument of type <typeparamref name="TRequest"/>.</exception>
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder) where TRequest : class =>
        builder.AddEndpointFilter(static async (context, next) =>
        {
            var request = context.Arguments.OfType<TRequest>().FirstOrDefault()
                ?? throw new InvalidOperationException($"The endpoint has no {typeof(TRequest).Name} argument to validate.");

            return IsValid(request) ? await next(context) : Results.BadRequest(ErrorResponse.InvalidRequest());
        });
}
