using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;
using Polly;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>Registers the OpenEMR identity provider, its token verifier, and their circuit breakers.</summary>
public static class OpenEmrServiceCollectionExtensions
{
    /// <summary>How long a request to OpenEMR may take before it fails.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The name of the HTTP client that fetches OpenEMR's JWKS.</summary>
    private const string JwksClientName = "OpenEmrJwks";

    /// <summary>
    /// Registers <see cref="IIdentityProvider"/> as a circuit-breaking decorator over the OpenEMR client.
    /// The client reads <see cref="OpenEmrOptions"/>, which the composition root binds and validates.
    /// </summary>
    /// <remarks>
    /// The client uses a 15-second timeout. It accepts any server certificate only when
    /// <c>OpenEmr:AcceptAnyServerCertificate</c> is <c>true</c>. The API host refuses that value outside Development.
    /// The breaker's state is held in one shared pipeline, so it spans requests. Retries are not used, because
    /// repeating a password grant would send extra credential attempts to OpenEMR.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddOpenEmrIdentityProvider(this IServiceCollection services)
    {
        services.AddHttpClient<OpenEmrIdentityProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = RequestTimeout;
        })
        .UseOpenEmrTls();

        services.AddSingleton(sp => CircuitBreakingIdentityProvider.CreatePipeline(BreakerSettings(sp)));

        // The wrapped client is transient, so the decorator is too. The pipeline above is the shared part.
        services.AddTransient<IIdentityProvider>(sp => new CircuitBreakingIdentityProvider(
            sp.GetRequiredService<OpenEmrIdentityProvider>(),
            sp.GetRequiredService<ResiliencePipeline<PasswordGrantResult>>()));

        return services;
    }

    /// <summary>
    /// Registers the verifier for OpenEMR-issued JWTs as a circuit-breaking decorator. The verifier fetches keys
    /// from OpenEMR's JWKS endpoint through its own HTTP client, with the same TLS setting as the identity provider.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddOpenEmrTokenVerifier(this IServiceCollection services)
    {
        services.AddHttpClient(JwksClientName).UseOpenEmrTls();

        services.AddSingleton(sp => new OpenEmrTokenVerifier(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(JwksClientName),
            sp.GetRequiredService<IOptions<OpenEmrOptions>>()));

        services.AddSingleton(sp => CircuitBreakingTokenVerifier.CreatePipeline(BreakerSettings(sp)));

        // The verifier holds the key cache, so it is a singleton. The breaker state is shared through its pipeline.
        services.AddSingleton<IExternalTokenVerifier>(sp => new CircuitBreakingTokenVerifier(
            sp.GetRequiredService<OpenEmrTokenVerifier>(),
            sp.GetRequiredService<ResiliencePipeline<VerifiedToken?>>()));

        return services;
    }

    /// <summary>Reads the circuit breaker settings from the bound <c>OpenEmr</c> options.</summary>
    private static CircuitBreakerOptions BreakerSettings(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value.CircuitBreaker;

    /// <summary>Sets the primary handler from <c>OpenEmr:AcceptAnyServerCertificate</c>, read when the handler is built.</summary>
    private static IHttpClientBuilder UseOpenEmrTls(this IHttpClientBuilder builder) =>
        builder.ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value;
            return CreateHandler(options.AcceptAnyServerCertificate);
        });

    /// <summary>Creates the primary handler. It accepts any certificate only when the option is set.</summary>
    private static HttpClientHandler CreateHandler(bool acceptAnyCertificate)
    {
        var handler = new HttpClientHandler();
        if (acceptAnyCertificate)
        {
            // OpenEMR's dev certificate is self-signed. Enabled only by configuration.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }
}
