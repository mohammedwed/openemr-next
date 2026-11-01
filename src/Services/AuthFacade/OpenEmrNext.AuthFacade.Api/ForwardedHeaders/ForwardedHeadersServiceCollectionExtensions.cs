using System.Net;
using Microsoft.Extensions.Options;

namespace OpenEmrNext.AuthFacade.Api.ForwardedHeaders;

/// <summary>
/// Trusts the gateway's <c>X-Forwarded-Proto</c> and <c>X-Forwarded-For</c> headers (ADR 0039), so a
/// request proxied through it is seen as the HTTPS request it actually was, and keyed by the real client
/// address rather than the gateway's own — both of which the gateway itself always sets (it never copies
/// an inbound client's own forwarded headers through, so there is nothing for a caller to spoof there).
/// <c>X-Forwarded-Host</c> is deliberately not trusted here: the gateway already rewrites the proxied
/// request's actual <c>Host</c> header for AuthFacade's routes (ADR 0034), which this would duplicate.
/// </summary>
/// <remarks>
/// This is a small hand-rolled middleware, not ASP.NET Core's own <c>UseForwardedHeaders</c>: built and
/// verified against it first, but <c>ForwardedHeadersOptions.KnownIPNetworks</c> did not actually restrict
/// anything against the installed 10.0.13 runtime in testing — a request from an address outside the
/// configured network still had its header honored. Confirmed in an isolated reproduction outside this
/// codebase, with <c>KnownProxies</c> cleared too, so it was not a leftover default. Hand-rolling the one
/// check this needs (is the immediate peer inside the trusted network?) is small enough to verify directly
/// rather than depend on that library behavior being fixed or understood. Revisit if a later runtime's
/// documented behavior is confirmed to differ.
/// </remarks>
public static class ForwardedHeadersServiceCollectionExtensions
{
    /// <summary>Registers and validates <see cref="TrustedProxyOptions"/> at startup.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddTrustedProxyForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<TrustedProxyOptions>()
            .BindConfiguration(TrustedProxyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Adds the forwarded-headers middleware, restricted to the already-validated trusted network. Must
    /// run before anything that reads <see cref="HttpContext.Connection"/>'s address or
    /// <see cref="HttpRequest.IsHttps"/> — in particular, before the login rate limiter (ADR 0038) and
    /// authentication, both of which depend on either one.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication UseTrustedProxyForwardedHeaders(this WebApplication app)
    {
        var trustedNetwork = IPNetwork.Parse(
            app.Services.GetRequiredService<IOptions<TrustedProxyOptions>>().Value.TrustedNetworkCidr);

        app.Use(async (context, next) =>
        {
            // Not the gateway's own chain-walking semantics (X-Forwarded-For as a comma-separated list of
            // hops) -- one gateway, one hop, so a single header value is all this ever needs to read.
            if (context.Connection.RemoteIpAddress is { } remoteAddress && trustedNetwork.Contains(remoteAddress))
            {
                if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var proto) && proto.Count == 1
                    && proto[0] is "http" or "https")
                {
                    context.Request.Scheme = proto[0]!;
                }

                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && forwardedFor.Count == 1
                    && IPAddress.TryParse(forwardedFor[0], out var realClientAddress))
                {
                    context.Connection.RemoteIpAddress = realClientAddress;
                }
            }

            await next(context);
        });

        return app;
    }
}
