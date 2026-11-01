using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api.Resilience;
using OpenEmrNext.AuthFacade.Composition;
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

        // Read directly rather than through IOptions<T>: BuildRegistration and UseSystemNetHttp's own
        // builder both need the pipelines built now, synchronously, not resolved per-request later (the
        // same reason `openEmr` above is read the same way). Still registered and validated at startup
        // (ADR 0041) so a bad threshold fails fast rather than silently falling back to the defaults here.
        var circuitBreaker = configuration.GetSection(CircuitBreakerOptions.SectionName).Get<CircuitBreakerOptions>()
            ?? new CircuitBreakerOptions();
        services.AddOptions<CircuitBreakerOptions>()
            .BindConfiguration(CircuitBreakerOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The real Compose stack configures AuthFacadeDb:* to point at a dedicated MariaDB database,
        // separate from OpenEMR's own, so both the state-token store and the Data Protection key ring
        // below survive a restart and (eventually) a second replica (ADR 0043, closing the gap ADR 0028
        // named). Nothing else configures that section -- tests, and a bare `dotnet run` outside Docker,
        // fall back to a fresh, uniquely-named SQLite file per process: losing it on restart is fine
        // there (the store only needs to survive one in-flight login in that case), and a fixed shared
        // path would let concurrent test hosts collide.
        var authFacadeDb = configuration.GetSection(AuthFacadeDbOptions.SectionName).Get<AuthFacadeDbOptions>()
            ?? new AuthFacadeDbOptions();
        // Computed once, here, rather than inside the AddDbContext callback below: that callback re-runs
        // on every new scope (AddDbContext's optionsLifetime defaults to Scoped, not Singleton), so a path
        // generated inside it would pick a fresh, schema-less file for every request after
        // EnsureAuthFacadeDatabaseCreatedAsync populated only the first one.
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"authfacade-{Guid.NewGuid():N}.sqlite3");
        services.AddDbContext<AuthFacadeDbContext>(options =>
        {
            if (authFacadeDb.IsConfigured)
            {
                var connectionString = "Server=" + authFacadeDb.Host +
                    ";Port=" + authFacadeDb.Port +
                    ";Database=" + authFacadeDb.Database +
                    ";User=" + authFacadeDb.User +
                    ";Password=" + authFacadeDb.Password + ";";
                options.UseMySql(connectionString, new MariaDbServerVersion(new Version(12, 3, 3))).UseOpenIddict();
            }
            else
            {
                options.UseSqlite($"Data Source={sqlitePath}").UseOpenIddict();
            }
        });

        // The AuthFacadeSession cookie's ticket (ADR 0027) is protected with these same keys, in the same
        // database as the state-token store above -- one dedicated store for everything session-related
        // (ADR 0043), nothing shared with OpenEMR's own data.
        services.AddDataProtection().PersistKeysToDbContext<AuthFacadeDbContext>();

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

        // Backs the session cookie with a server-side record (ADR 0044), so a session can actually be
        // revoked instead of only ever expiring on its own. A separate, post-registration step rather than
        // set inside AddCookie's own configure delegate above: AuthFacadeSessionStore needs
        // IServiceScopeFactory and IDataProtectionProvider, neither resolvable yet at this point -- the
        // builder pattern below runs once those are actually available from the built container.
        services.AddOptions<CookieAuthenticationOptions>(SessionScheme)
            .Configure<IServiceScopeFactory, IDataProtectionProvider>((options, scopeFactory, dataProtectionProvider) =>
            {
                options.SessionStore = new AuthFacadeSessionStore(scopeFactory, dataProtectionProvider);
            });

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthFacadeDbContext>())
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

                // Guards the token exchange (ADR 0041): once OpenEMR's token endpoint has failed enough
                // times, further attempts fail immediately instead of each one waiting out its own timeout.
                // A rejected grant (a 4xx -- a bad code, a replayed one) is not a failure here; only a
                // connection problem or a 5xx/408 opens the circuit.
                options.UseSystemNetHttp()
                       .SetHttpResiliencePipeline(OpenEmrCircuitBreakers.CreateHttpPipeline(circuitBreaker));

                options.AddRegistration(BuildRegistration(openEmr, circuitBreaker));
            });

        return services;
    }

    /// <summary>
    /// Creates AuthFacade's own database schema — OpenIddict's state-token store and the Data Protection
    /// key ring table alike. There is no migration history (<see cref="AuthFacadeDbContext"/>'s remarks
    /// explain why); call this once after the host is built and before it starts serving requests.
    /// </summary>
    /// <param name="app">The built application.</param>
    public static async Task EnsureAuthFacadeDatabaseCreatedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthFacadeDbContext>();
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
    private static OpenIddictClientRegistration BuildRegistration(OpenEmrOptions openEmr, CircuitBreakerOptions circuitBreaker)
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
            // OpenEmrTokenVerifier already uses for the equivalent bearer-token JWKS fetch. A separate
            // circuit breaker instance from the token exchange's (ADR 0041): same thresholds, but its own
            // state, since a failing JWKS endpoint says nothing about whether the token endpoint is up.
            ConfigurationManager = new OpenEmrConfigurationManager(
                new HttpClient(), configuration, jwksUri, OpenEmrCircuitBreakers.CreateJwksPipeline(circuitBreaker))
        };
    }
}
