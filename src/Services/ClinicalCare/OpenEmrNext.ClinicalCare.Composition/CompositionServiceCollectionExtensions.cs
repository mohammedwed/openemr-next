using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenEmrNext.ClinicalCare.Composition;

/// <summary>
/// The composition root the host calls, and the only place configuration is read.
/// </summary>
public static class CompositionServiceCollectionExtensions
{
    /// <summary>Binds and validates every configuration section this service needs.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration holding the <c>ClinicalCareDb</c> and <c>InternalGateway</c> sections.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddClinicalCare(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ClinicalCareDbOptions>()
            .Bind(configuration.GetSection(ClinicalCareDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<InternalGatewayOptions>()
            .Bind(configuration.GetSection(InternalGatewayOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<InternalEventsOptions>()
            .Bind(configuration.GetSection(InternalEventsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
