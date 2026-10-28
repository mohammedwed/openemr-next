using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api.Contracts.Responses;

namespace OpenEmrNext.AuthFacade.Api.RateLimiting;

/// <summary>Registers the rate limiter that guards the token endpoint against password guessing.</summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>The policy name that <c>POST /api/v1/connect/token</c> requires.</summary>
    public const string LoginPolicy = "login";

    /// <summary>
    /// Registers a fixed-window limiter per client address for the login policy. The limit comes from
    /// <see cref="LoginRateLimitOptions"/> and is read on each request, so validation runs at startup.
    /// Requests over the limit get 429 with a <c>Retry-After</c> header and no queueing.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<LoginRateLimitOptions>()
            .BindConfiguration(LoginRateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    ErrorResponse.TooManyRequests(),
                    cancellationToken);
            };

            limiter.AddPolicy(LoginPolicy, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;
                var clientAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientAddress, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = options.Window,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return services;
    }
}
