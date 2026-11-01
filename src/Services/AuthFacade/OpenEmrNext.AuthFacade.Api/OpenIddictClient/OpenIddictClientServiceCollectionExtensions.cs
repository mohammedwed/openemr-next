using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenEmrNext.AuthFacade.Api.OpenIddictClient;

/// <summary>
/// Registers AuthFacade as an OpenIddict OIDC client of OpenEMR's own, already-conformant OAuth2/OIDC
/// server — the browser authorization-code flow with PKCE (ADR 0001's stated production target), driven by
/// a library instead of hand-rolled PKCE/cookie code. OpenEMR remains the only OIDC provider; AuthFacade is
/// not one.
/// </summary>
public static class OpenIddictClientServiceCollectionExtensions
{
    /// <summary>The scheme that holds the local session cookie after a successful login.</summary>
    public const string SessionScheme = "AuthFacadeSession";

    /// <summary>
    /// The scope requested — the scope the <c>authfacade</c> client is seeded with (ADR 0005), not
    /// <c>OpenEmr:Scope</c>, which is the password grant's <c>openid api:oemr</c>.
    /// </summary>
    private static readonly string[] RequestedScopes = [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess];

    /// <summary>
    /// Registers the OpenIddict client against OpenEMR, its state-token store, and the cookie scheme that
    /// holds the resulting local session.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that holds the already-validated <c>OpenEmr</c> section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddOpenIddictClient(this IServiceCollection services, IConfiguration configuration)
    {
        var openEmr = configuration.GetSection(OpenEmrOptions.SectionName).Get<OpenEmrOptions>()
            ?? throw new InvalidOperationException($"The '{OpenEmrOptions.SectionName}' configuration section is missing.");

        // A fresh, uniquely-named file per process (not a fixed path): the store only needs to survive one
        // in-flight login, so losing it on restart is fine (the same reasoning ADR 0024 already applied to
        // its Data Protection key ring), and a fixed shared path would let concurrent test hosts collide.
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"authfacade-openiddict-client-{Guid.NewGuid():N}.sqlite3");
        services.AddDbContext<ClientDbContext>(options => options.UseSqlite($"Data Source={sqlitePath}").UseOpenIddict());

        // The primary AddAuthentication() call: it sets DefaultScheme to this cookie scheme, so /me
        // (and anything else with no explicit scheme named) resolves to it.
        services.AddAuthentication(SessionScheme)
            .AddCookie(SessionScheme, options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.SlidingExpiration = false;
                // This scheme is only ever used by explicit SignInAsync/SignOutAsync calls from /callback
                // and /logout; it never needs to redirect anyone to a login page on its own.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<ClientDbContext>())
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow();

                // Dev-only ephemeral certificates. A real deployment needs persisted ones; ADR 0020
                // already treats this stack as local-only, so this matches its existing maturity level.
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .EnableRedirectionEndpointPassthrough()
                       // Without this, OpenIddict writes its own plain-text/JSON error response for a
                       // failed callback (e.g. a rejected grant, a bad nonce) directly, short-circuiting the
                       // pipeline before AuthenticateAsync ever returns to HandleCallbackAsync — so the
                       // generic "login_failed" redirect this app relies on (ADR 0026) never runs, and
                       // OpenIddict's own error text leaks to the browser instead. This passes the error
                       // through to AuthenticateAsync as a failed AuthenticateResult, same as any other
                       // authentication failure, so the endpoint's own error handling takes over.
                       .EnableErrorPassthrough()
                       // The local stack runs over plain HTTP (ADR 0020); OpenIddict otherwise refuses to
                       // challenge outside HTTPS. DisableTransportSecurityRequirement alone is not enough:
                       // OpenIddict's own correlation cookie still defaults to SameSite=None with Secure
                       // forced on (SecurePolicy.Always — None requires Secure by spec), so a real browser
                       // never sends it back to a plain-HTTP /callback (confirmed: the DB-backed state
                       // token round-tripped fine, but ID2129 "No correlation cookie" still fired). Lax
                       // does not require Secure and is still sent on the top-level redirect back from
                       // OpenEMR, the same SameSite=Lax reasoning ADR 0024 already used for its own
                       // cookies; SecurePolicy.SameAsRequest keeps it Secure on a real HTTPS deployment.
                       .DisableTransportSecurityRequirement()
                       .Configure(o =>
                       {
                           o.CookieBuilder.SameSite = SameSiteMode.Lax;
                           o.CookieBuilder.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                       });

                options.UseSystemNetHttp();

                options.AddRegistration(BuildRegistration(openEmr));
            });

        return services;
    }

    /// <summary>
    /// Creates the state-token store's schema. There is no migration history (<see cref="ClientDbContext"/>'s
    /// remarks explain why); call this once after the host is built and before it starts serving requests.
    /// </summary>
    /// <param name="app">The built application.</param>
    public static async Task EnsureOpenIddictClientDatabaseCreatedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Builds the one client registration against OpenEMR, with the discovery document's automatic
    /// endpoint lookup bypassed: the authorize endpoint must be the publicly-reachable <c>OpenEmr:Issuer</c>
    /// address (the browser follows it), while the token and JWKS endpoints must be the container-internal
    /// <c>OpenEmr:BaseUrl</c> address (AuthFacade calls them itself) — the same split ADR 0012 and ADR 0024
    /// already needed, and the same bug this would silently reproduce if left to discovery, since OpenEMR's
    /// own discovery document only ever publishes the single public host for every endpoint.
    /// </summary>
    private static OpenIddictClientRegistration BuildRegistration(OpenEmrOptions openEmr)
    {
        // No trailing slash: this must byte-for-byte match the "iss" claim OpenEMR actually writes into its
        // tokens (confirmed against OpenEmrTokenVerifier, which already validates that successfully), or
        // id_token validation fails with ID2088 "issuer not valid" even though the token itself is genuine.
        var issuerString = openEmr.Issuer.TrimEnd('/');
        var issuer = new Uri(issuerString, UriKind.Absolute);
        var internalBase = openEmr.BaseUrl.TrimEnd('/') + "/oauth2/default";
        var jwksUri = new Uri($"{internalBase}/jwk", UriKind.Absolute);

        var configuration = new OpenIddictConfiguration
        {
            Issuer = issuer,
            AuthorizationEndpoint = new Uri($"{issuerString}/authorize", UriKind.Absolute),
            TokenEndpoint = new Uri($"{internalBase}/token", UriKind.Absolute),
            GrantTypesSupported = { GrantTypes.AuthorizationCode },
            ResponseTypesSupported = { ResponseTypes.Code },
            ResponseModesSupported = { ResponseModes.Query },
            CodeChallengeMethodsSupported = { CodeChallengeMethods.Sha256 }
        };

        return new OpenIddictClientRegistration
        {
            Issuer = issuer,
            ClientId = openEmr.ClientId,
            ClientSecret = openEmr.ClientSecret,
            RedirectUri = new Uri(openEmr.RedirectUri, UriKind.Absolute),
            Scopes = { RequestedScopes[0], RequestedScopes[1], RequestedScopes[2], RequestedScopes[3] },

            // Not Configuration: that gets wrapped in a StaticConfigurationManager, which never fetches
            // anything, so JsonWebKeySetUri on it would never actually be followed (see
            // OpenEmrConfigurationManager's remarks — this was ID2090's root cause). The HttpClient is
            // owned by the manager for the app's lifetime, the same long-lived-singleton pattern
            // OpenEmrTokenVerifier already uses for the equivalent bearer-token JWKS fetch.
            ConfigurationManager = new OpenEmrConfigurationManager(new HttpClient(), configuration, jwksUri)
        };
    }
}
