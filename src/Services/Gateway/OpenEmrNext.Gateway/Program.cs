using OpenEmrNext.Gateway;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

// The single external origin a browser talks to (ADR 0033): WebApp (the dashboard), AuthFacade, and
// PatientManagement need to share one origin -- AuthFacade for the AuthFacadeSession cookie to work on
// the dashboard's own fetch calls (without CORS or loosening SameSite), PatientManagement because it
// trusts no caller that isn't this gateway (ADR 0055). This gateway is that origin -- it routes each
// service's own known path prefixes to it and everything else to WebApp's static files. It still makes
// no auth decisions for AuthFacade's own routes (ADR 0029/0030); for PatientManagement's, the one thing
// it does is attach the shared secret server-side (see the "patientmanagement" case in AddTransforms
// below) -- not a real per-user authorization decision, just the one ADR 0055 already scoped to this
// gateway.
var builder = WebApplication.CreateBuilder(args);

var authFacadeBaseUrl = builder.Configuration["AuthFacade:BaseUrl"]
    ?? throw new InvalidOperationException("AuthFacade:BaseUrl is required.");
var webAppBaseUrl = builder.Configuration["WebApp:BaseUrl"]
    ?? throw new InvalidOperationException("WebApp:BaseUrl is required.");
var patientManagementBaseUrl = builder.Configuration["PatientManagement:BaseUrl"]
    ?? throw new InvalidOperationException("PatientManagement:BaseUrl is required.");
var internalGatewaySecret = builder.Configuration["InternalGateway:Secret"]
    ?? throw new InvalidOperationException("InternalGateway:Secret is required.");
var clinicalCareBaseUrl = builder.Configuration["ClinicalCare:BaseUrl"]
    ?? throw new InvalidOperationException("ClinicalCare:BaseUrl is required.");
// Its own secret, not PatientManagement's (API-16, ADR 0064): attached only on requests forwarded to the
// clinicalcare cluster, so neither backend can present itself to the other as this gateway.
var clinicalCareSecret = builder.Configuration["ClinicalCare:Secret"]
    ?? throw new InvalidOperationException("ClinicalCare:Secret is required.");

// Every path AuthFacade itself maps outside /api/v1 (UserEndpoints, OpenIddictClientEndpoints,
// LogoutEndpoints, SessionEndpoints, OpenEmrBffProxyEndpoints, SystemEndpoints) plus /api/v1 itself. More specific than WebApp's own
// catch-all below, so normal ASP.NET Core route precedence sends a request here first regardless of
// registration order -- no exclusion pattern needed, unlike AuthFacadeEndpoints' own SPA fallback
// attempt, which fought the same problem with a fragile regex instead of plain route specificity.
string[] authFacadeRoutePatterns =
[
    "/api/{**catch-all}",
    "/oidc/{**catch-all}",
    "/callback",
    "/logout",
    "/sessions/{**catch-all}",
    "/bff/{**catch-all}",
    "/health",
    "/openapi/{**catch-all}",
    "/scalar/{**catch-all}"
];

// PatientManagement's own path prefixes: /api/v1/patients (registration, insurance, advance
// directives -- PatientEndpoints/InsuranceEndpoints/AdvanceDirectiveEndpoints all map under the same
// /api/v1/patients group) and /fhir (the FHIR Patient API, a top-level group of its own). Both more
// specific than AuthFacade's bare /api/{**catch-all} above, so the same route-precedence reasoning
// already stated for that one applies here too.
// /fhir is shared by two services since ADR 0079: Patient Management serves Patient, Clinical Care serves the clinical resources, so the pattern is per resource type and not a catch-all
// (a new FHIR resource must be routed here, and the Clinical Care suite's GatewayRoutesTests fails if it is not).
string[] patientManagementRoutePatterns = ["/api/v1/patients/{**catch-all}", "/fhir/Patient/{**catch-all}"];

// ClinicalCare's own path prefix: /api/v1/encounters. A flat prefix of its own rather than
// /api/v1/patients/{uuid}/encounters, because everything under /api/v1/patients/ already belongs to
// PatientManagement and one prefix must belong to one service (ADR 0064). More specific than AuthFacade's
// bare /api/{**catch-all}, so the same route-precedence reasoning applies.
// Vitals and SOAP notes live under /encounters; immunizations and the encounter lookups have prefixes of their own. A test in the Clinical Care suite
// (GatewayRoutesTests) fails if a prefix its API serves is missing here, so a new route group cannot be built and left unreachable.
string[] clinicalCareRoutePatterns =
[
    "/api/v1/encounters/{**catch-all}", "/api/v1/problems/{**catch-all}", "/api/v1/allergies/{**catch-all}",
    "/api/v1/immunizations/{**catch-all}", "/api/v1/lookups/{**catch-all}",
    "/fhir/Immunization/{**catch-all}"
];

// The only routes that need a signed-in user (ADR 0075): the two services that hold clinical and patient data. AuthFacade's own routes are how a user signs in, and WebApp's are static files.
var protectedPaths = new ProtectedPaths(patientManagementRoutePatterns.Concat(clinicalCareRoutePatterns));

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
    .Concat(patientManagementRoutePatterns.Select((pattern, index) => new RouteConfig
    {
        RouteId = $"patientmanagement-{index}",
        ClusterId = "patientmanagement",
        Match = new RouteMatch { Path = pattern }
    }))
    .Concat(clinicalCareRoutePatterns.Select((pattern, index) => new RouteConfig
    {
        RouteId = $"clinicalcare-{index}",
        ClusterId = "clinicalcare",
        Match = new RouteMatch { Path = pattern }
    }))
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
        ClusterId = "patientmanagement",
        Destinations = new Dictionary<string, DestinationConfig> { ["patientmanagement"] = new() { Address = patientManagementBaseUrl } }
    },
    new ClusterConfig
    {
        ClusterId = "clinicalcare",
        Destinations = new Dictionary<string, DestinationConfig> { ["clinicalcare"] = new() { Address = clinicalCareBaseUrl } }
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

        // The verified user is only ever set by the session check below. Any value a caller sends is removed on every route, so no service can be handed an identity this gateway did not verify (ADR 0075).
        transformContext.AddRequestTransform(requestTransformContext =>
        {
            requestTransformContext.ProxyRequest.Headers.Remove(SessionVerifier.VerifiedUserHeader);
            if (transformContext.Cluster?.ClusterId is "patientmanagement" or "clinicalcare")
            {
                // The two data services take the verified user from the header and never need the browser's session cookie, so it is not handed to them: a credential they cannot use is one they cannot leak.
                requestTransformContext.ProxyRequest.Headers.Remove("Cookie");
                if (requestTransformContext.HttpContext.Items[SessionIdentityExtensions.UserItem] is Guid user)
                {
                    requestTransformContext.ProxyRequest.Headers.Add(SessionVerifier.VerifiedUserHeader, user.ToString());
                }
            }

            return ValueTask.CompletedTask;
        });
        transformContext.AddResponseHeader("Strict-Transport-Security", "max-age=2592000", append: false, ResponseCondition.Always);

        // Scoped to only the patientmanagement cluster, not applied globally like the transforms
        // above: the secret must never reach AuthFacade or WebApp's destinations, only the one
        // service ADR 0055 requires it for. Unconditional -- this gateway makes no per-user auth
        // decision here, only the one ADR 0055 already scoped to it (see the top-of-file comment).
        if (transformContext.Cluster?.ClusterId == "patientmanagement")
        {
            transformContext.AddRequestTransform(requestTransformContext =>
            {
                requestTransformContext.ProxyRequest.Headers.Remove("X-Internal-Gateway-Secret");
                requestTransformContext.ProxyRequest.Headers.Add("X-Internal-Gateway-Secret", internalGatewaySecret);
                return ValueTask.CompletedTask;
            });
        }

        // The same pattern, with ClinicalCare's own secret (API-16). The header is deleted before it is set,
        // so a caller can never smuggle in a value of its own.
        if (transformContext.Cluster?.ClusterId == "clinicalcare")
        {
            transformContext.AddRequestTransform(requestTransformContext =>
            {
                requestTransformContext.ProxyRequest.Headers.Remove("X-Internal-Gateway-Secret");
                requestTransformContext.ProxyRequest.Headers.Add("X-Internal-Gateway-Secret", clinicalCareSecret);
                return ValueTask.CompletedTask;
            });
        }
    });

// The session check calls AuthFacade over its internal address, with a short timeout: a slow sign-in service fails closed (503) rather than holding every request.
builder.Services.AddSingleton<SessionVerifier>();
builder.Services.AddHttpClient(SessionVerifier.ClientName, client =>
{
    client.BaseAddress = new Uri(authFacadeBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
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

// After the redirect, before the proxy: a request to a protected route is checked (401 or 503) before it is forwarded.
app.UseSessionIdentity(protectedPaths);

app.MapReverseProxy();
app.Run();

/// <summary>Lets the in-process gateway tests start the real application.</summary>
public partial class Program;
