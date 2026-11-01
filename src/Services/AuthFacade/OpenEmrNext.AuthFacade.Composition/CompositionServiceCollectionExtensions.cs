using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenEmrNext.AuthFacade.Composition;

/// <summary>
/// The composition root that every AuthFacade host calls, and the only place configuration is read.
/// </summary>
public static class CompositionServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates the <c>OpenEmr</c> section. Each host builds its own OpenIddict client,
    /// BFF proxy and transport on top, reading the bound <see cref="OpenEmrOptions"/>.
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

        return services;
    }
}
