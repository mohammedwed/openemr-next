using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Application;
using OpenEmrNext.AuthFacade.Infrastructure;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

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
    /// <param name="configuration">The configuration that holds the <c>OpenEmr</c> and <c>Jwt</c> sections.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddAuthFacade(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenEmrOptions>()
            .Bind(configuration.GetSection(OpenEmrOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services
            .AddApplication()
            .AddInfrastructure();
    }
}
