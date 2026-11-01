using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

// The single external origin a browser talks to (ADR 0033): WebApp (the dashboard) and AuthFacade need to
// share one origin for the AuthFacadeSession cookie to work on the dashboard's own fetch calls, without
// CORS or loosening SameSite. This gateway is that origin -- it routes AuthFacade's own known path
// prefixes to it and everything else to WebApp's static files, and otherwise does nothing: no auth
// decisions, no token handling, consistent with AuthFacade keeping those (ADR 0029/0030).
var builder = WebApplication.CreateBuilder(args);

var authFacadeBaseUrl = builder.Configuration["AuthFacade:BaseUrl"]
    ?? throw new InvalidOperationException("AuthFacade:BaseUrl is required.");
var webAppBaseUrl = builder.Configuration["WebApp:BaseUrl"]
    ?? throw new InvalidOperationException("WebApp:BaseUrl is required.");

// Every path AuthFacade itself maps outside /api/v1 (UserEndpoints, OpenIddictClientEndpoints,
// LogoutEndpoints, OpenEmrBffProxyEndpoints, SystemEndpoints) plus /api/v1 itself. More specific than WebApp's own
// catch-all below, so normal ASP.NET Core route precedence sends a request here first regardless of
// registration order -- no exclusion pattern needed, unlike AuthFacadeEndpoints' own SPA fallback
// attempt, which fought the same problem with a fragile regex instead of plain route specificity.
string[] authFacadeRoutePatterns =
[
    "/api/{**catch-all}",
    "/oidc/{**catch-all}",
    "/callback",
    "/logout",
    "/bff/{**catch-all}",
    "/health",
    "/openapi/{**catch-all}",
    "/scalar/{**catch-all}"
];

var routes = authFacadeRoutePatterns
    .Select((pattern, index) => new RouteConfig
    {
        RouteId = $"authfacade-{index}",
        ClusterId = "authfacade",
        Match = new RouteMatch { Path = pattern },
        // Without this, YARP sends the destination's own authority ("auth-facade:8080") as the Host
        // header, not this gateway's ("localhost:7130"). OpenIddict.Client recognizes /callback as its
        // own registered redirection endpoint by comparing the request it actually receives against the
        // registered RedirectUri (ADR 0033's whole point: that redirect_uri is this gateway's address,
        // not AuthFacade's own) -- with the wrong Host, that comparison fails and /callback throws
        // "endpoints that are not managed by the OpenIddict client stack", even though the state token
        // itself (looked up by the state query parameter, independent of Host) was found correctly.
        Transforms = [new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }]
    })
    .Append(new RouteConfig
    {
        RouteId = "webapp",
        ClusterId = "webapp",
        Match = new RouteMatch { Path = "/{**catch-all}" }
    })
    .ToArray();

ClusterConfig[] clusters =
[
    new ClusterConfig
    {
        ClusterId = "authfacade",
        Destinations = new Dictionary<string, DestinationConfig> { ["authfacade"] = new() { Address = authFacadeBaseUrl } }
    },
    new ClusterConfig
    {
        ClusterId = "webapp",
        Destinations = new Dictionary<string, DestinationConfig> { ["webapp"] = new() { Address = webAppBaseUrl } }
    }
];

// Every route forwarded to AuthFacade carries the real scheme and client address (ADR 0039): AuthFacade's
// own forwarded-headers middleware trusts exactly this gateway's network to read them, which is what lets
// the session cookie's Secure flag and the per-address login rate limit (ADR 0038) both see the truth
// instead of this gateway's own scheme and address. Registered once, globally -- it applies to every
// route this proxy builds, WebApp's included, the same way the BFF proxy's own token-attaching transform
// (OpenEmrBffProxyServiceCollectionExtensions) applies globally despite that proxy having only one route.
//
// The HSTS header is added here too, not via app.UseHsts(): this app does nothing but proxy (every
// request matches MapReverseProxy, there is no other endpoint), so the backend's own response headers
// are always what a client actually sees -- middleware-set response headers never survive being copied
// over by the proxied response. Confirmed by testing: with only UseHsts() in the pipeline, the header
// never reached the client at all.
builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters)
    .AddTransforms(transformContext =>
    {
        transformContext.AddXForwardedFor().AddXForwardedProto();
        transformContext.AddResponseHeader("Strict-Transport-Security", "max-age=2592000", append: false, ResponseCondition.Always);
    });

// HttpsRedirection:HttpsPort is the externally-published HTTPS port (GATEWAY_HTTPS_PORT), not Kestrel's
// own internal 8443: the redirect's Location header must name a port the browser can actually reach.
builder.Services.AddHttpsRedirection(options =>
{
    var httpsPort = builder.Configuration.GetValue<int?>("HttpsRedirection:HttpsPort");
    if (httpsPort is not null)
    {
        options.HttpsPort = httpsPort;
    }
});

var app = builder.Build();

// Redirects any plain-HTTP request to HTTPS (ADR 0039), before the proxy itself runs, so neither
// AuthFacade nor WebApp ever see a request this gateway hasn't already upgraded. HSTS is a response
// transform above, not middleware here -- see that comment for why.
app.UseHttpsRedirection();

app.MapReverseProxy();
app.Run();
