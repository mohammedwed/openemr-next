using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>Registers the OpenEMR identity provider and its options.</summary>
public static class OpenEmrServiceCollectionExtensions
{
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
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (environment.IsDevelopment())
            {
                // OpenEMR's dev certificate is self-signed. Development only.
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

        return services;
    }
}
