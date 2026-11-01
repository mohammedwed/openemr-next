using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenEmrNext.AuthFacade.Api.Audit;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenEmrNext.AuthFacade.Composition;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace OpenEmrNext.AuthFacade.Api.Proxy;

/// <summary>
/// Registers the reverse proxy that lets a browser session reach OpenEMR's own API without ever holding a
/// bearer token itself — the token-handling proxy ADR 0027 deferred. Every request under
/// <see cref="RoutePrefix"/> is forwarded to <c>OpenEmr:BaseUrl</c> at the same remaining path, with the real
/// OpenEMR access token the session cookie carries (ADR 0027) attached as <c>Authorization: Bearer</c>.
/// OpenEMR's response is relayed back unchanged, the same passthrough principle ADR 0013 already applied to
/// the token endpoint. Every call is also recorded to the audit trail (ADR 0042): this is the one place a
/// signed-in session actually touches OpenEMR's data.
/// </summary>
public static class OpenEmrBffProxyServiceCollectionExtensions
{
    /// <summary>The path prefix a browser calls; everything after it reaches OpenEMR at the same path.</summary>
    public const string RoutePrefix = "/bff/openemr";

    private const string RouteId = "openemr";

    /// <summary>
    /// Builds the single route/cluster pair from <c>OpenEmr:BaseUrl</c> and registers the request transform
    /// that attaches the session's access token.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that holds the already-validated <c>OpenEmr</c> section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddOpenEmrBffProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var openEmr = configuration.GetSection(OpenEmrOptions.SectionName).Get<OpenEmrOptions>()
            ?? throw new InvalidOperationException($"The '{OpenEmrOptions.SectionName}' configuration section is missing.");

        var route = new RouteConfig
        {
            RouteId = RouteId,
            ClusterId = RouteId,
            Match = new RouteMatch { Path = $"{RoutePrefix}/{{**catch-all}}" },
            // Declarative transform (the same shape as the appsettings.json "Transforms" array): strips the
            // prefix a browser calls so OpenEMR sees the real path it actually exposes.
            Transforms = [new Dictionary<string, string> { ["PathRemovePrefix"] = RoutePrefix }]
        };

        var cluster = new ClusterConfig
        {
            ClusterId = RouteId,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                [RouteId] = new() { Address = openEmr.BaseUrl }
            }
        };

        services.AddReverseProxy()
            .LoadFromMemory([route], [cluster])
            .AddTransforms(builderContext =>
            {
                builderContext.AddRequestTransform(async transformContext =>
                {
                    // The only credential a browser session holds is the AuthFacadeSession cookie (ADR
                    // 0024/0026); the real OpenEMR token it carries (ADR 0027) is attached here, server-side, so
                    // OpenEMR sees an ordinary bearer-authenticated request. No token on the session (a ticket
                    // that predates ADR 0027, or one OpenEMR never issued a refresh/access pair for) means no
                    // Authorization header is attached at all -- OpenEMR then rejects the request itself; this
                    // proxy makes no auth decision of its own (ADR 0013's passthrough).
                    var accessToken = await transformContext.HttpContext.GetTokenAsync(
                        OpenIddictClientServiceCollectionExtensions.SessionScheme, "access_token");

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    }
                });

                // Every call that reaches this proxy touches real OpenEMR data, so it goes to the audit
                // trail (ADR 0042): who (the session's subject), what (method and the path the browser
                // called, not OpenEMR's own rewritten one), and the outcome -- never the request or
                // response body, and never the token this same transform step attached above.
                builderContext.AddResponseTransform(transformContext =>
                {
                    var auditLog = transformContext.HttpContext.RequestServices.GetRequiredService<ILogger<AuditLog>>();
                    var subject = transformContext.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "unknown";
                    var statusCode = transformContext.ProxyResponse is null ? 0 : (int)transformContext.ProxyResponse.StatusCode;

                    auditLog.ProxyAccess(subject, transformContext.HttpContext.Request.Method, transformContext.HttpContext.Request.Path, statusCode);

                    return default;
                });
            });

        return services;
    }
}
