using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>Registers the OpenEMR identity provider and its options.</summary>
public static class OpenEmrServiceCollectionExtensions
{
    /// <summary>How long a request to OpenEMR may take before it fails.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Registers <see cref="IOpenEmrIdentityProvider"/> with a typed HTTP client. The client reads
    /// <see cref="OpenEmrOptions"/>, which the composition root binds and validates.
    /// </summary>
    /// <remarks>
    /// The client uses a 15-second timeout. It accepts any server certificate only when
    /// <c>OpenEmr:AcceptAnyServerCertificate</c> is <c>true</c>, which is set only in development settings.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddOpenEmrIdentityProvider(this IServiceCollection services)
    {
        services.AddHttpClient<IOpenEmrIdentityProvider, OpenEmrIdentityProvider>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = RequestTimeout;
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenEmrOptions>>().Value;
            return CreateHandler(options.AcceptAnyServerCertificate);
        });

        return services;
    }

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
