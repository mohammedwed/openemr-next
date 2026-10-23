using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>Registers the OpenEMR identity provider and its options.</summary>
public static class OpenEmrServiceCollectionExtensions
{
    /// <summary>How long a request to OpenEMR may take before it fails.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Binds and validates <see cref="OpenEmrOptions"/> at startup, then registers
    /// <see cref="IOpenEmrIdentityProvider"/> with a typed HTTP client.
    /// </summary>
    /// <remarks>
    /// The startup check fails the host when required settings are missing. The client uses a
    /// 15-second timeout. In the Development environment only, it accepts any server
    /// certificate, because OpenEMR's development certificate is self-signed.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that holds the <c>OpenEmr</c> section.</param>
    /// <param name="environment">The host environment, used to enable the development certificate bypass.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddOpenEmrIdentityProvider(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<OpenEmrOptions>()
            .Bind(configuration.GetSection(OpenEmrOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<IOpenEmrIdentityProvider, OpenEmrIdentityProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = RequestTimeout;
        })
        .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(environment));

        return services;
    }

    /// <summary>Creates the primary handler. Only the Development environment accepts any certificate.</summary>
    private static HttpClientHandler CreateHandler(IHostEnvironment environment)
    {
        var handler = new HttpClientHandler();
        if (environment.IsDevelopment())
        {
            // OpenEMR's dev certificate is self-signed. Development only.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    }
}
