using Yarp.ReverseProxy.Configuration;

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

// Every path AuthFacade itself maps outside /api/v1 (TokenEndpoints, UserEndpoints, OpenIddictClientEndpoints,
// OpenEmrBffProxyEndpoints, SystemEndpoints) plus /api/v1 itself. More specific than WebApp's own
// catch-all below, so normal ASP.NET Core route precedence sends a request here first regardless of
// registration order -- no exclusion pattern needed, unlike AuthFacadeEndpoints' own SPA fallback
// attempt, which fought the same problem with a fragile regex instead of plain route specificity.
string[] authFacadeRoutePatterns =
[
    "/api/{**catch-all}",
    "/oidc/{**catch-all}",
    "/callback",
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
        Match = new RouteMatch { Path = pattern }
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

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

var app = builder.Build();
app.MapReverseProxy();
app.Run();
