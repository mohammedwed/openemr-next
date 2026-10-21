using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

public static class OpenEmrServiceCollectionExtensions
{
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
