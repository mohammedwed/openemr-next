using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Application;
using OpenEmrNext.AuthFacade.Infrastructure;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.Composition;

/// <summary>
/// The composition root that every AuthFacade host calls. It calls each layer's entry point,
/// and registers nothing that depends on a host.
/// </summary>
public static class CompositionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application and Infrastructure layers. Each host adds its own bearer
    /// validation and transport on top.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that holds the <c>OpenEmr</c> section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAuthFacade(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenEmrOptions>()
            .Bind(configuration.GetSection(OpenEmrOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .AddApplication()
            .AddInfrastructure();
    }
}
