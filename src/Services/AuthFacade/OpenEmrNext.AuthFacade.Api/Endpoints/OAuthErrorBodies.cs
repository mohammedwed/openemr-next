using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;

namespace OpenEmrNext.AuthFacade.Api.Endpoints;

/// <summary>
/// Gives an OAuth-style body to the empty 400 ASP.NET Core sends when a form field fails to bind.
/// Endpoints marked with <see cref="WithOAuthErrorBodies"/> answer <c>{"error":"invalid_request"}</c> instead.
/// A request that is not a form never reaches the endpoint: routing answers 415 itself, with no body.
/// </summary>
public static class OAuthErrorBodies
{
    /// <summary>Marks an endpoint whose binding failures get an <c>invalid_request</c> body.</summary>
    private sealed class Marker;

    /// <summary>
    /// Makes binding failures return an empty 400 in every environment. ASP.NET Core's default in Development is
    /// to throw, which puts the exception text in the response body and leaves nothing for
    /// <see cref="UseOAuthErrorBodies"/> to fill in. Call this with <see cref="UseOAuthErrorBodies"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddOAuthErrorBodies(this IServiceCollection services) =>
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

    /// <summary>Marks the endpoint so <see cref="UseOAuthErrorBodies"/> fills in its binding failures.</summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder WithOAuthErrorBodies<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new Marker());

    /// <summary>
    /// Adds the middleware that writes <c>{"error":"invalid_request"}</c> for a 400 with no body on a marked
    /// endpoint. Responses that already have a body, such as <c>invalid_grant</c> or 429, are left alone.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><see cref="AddOAuthErrorBodies"/> was not called.</exception>
    public static IApplicationBuilder UseOAuthErrorBodies(this IApplicationBuilder app)
    {
        if (app.ApplicationServices.GetRequiredService<IOptions<RouteHandlerOptions>>().Value.ThrowOnBadRequest)
        {
            throw new InvalidOperationException(
                $"Call {nameof(AddOAuthErrorBodies)} before {nameof(UseOAuthErrorBodies)}: binding failures would throw instead of returning an empty 400.");
        }

        return app.Use(async (http, next) =>
        {
            await next(http);

            var response = http.Response;
            var bindingFailed = response.StatusCode == StatusCodes.Status400BadRequest;

            // An empty response has no content type. The framework may also have set Content-Length to 0,
            // which has to be cleared before a body can be written.
            var empty = !response.HasStarted && response.ContentType is null && response.ContentLength is null or 0;

            if (bindingFailed && empty && http.GetEndpoint()?.Metadata.GetMetadata<Marker>() is not null)
            {
                response.ContentLength = null;
                await response.WriteAsJsonAsync(ErrorResponse.InvalidRequest());
            }
        });
    }
}
