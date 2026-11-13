using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenEmrNext.PatientManagement.Composition;

/// <summary>
/// The composition root the host calls, and the only place configuration is read.
/// </summary>
public static class CompositionServiceCollectionExtensions
{
    /// <summary>Binds and validates the <c>PatientManagementDb</c> section.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The configuration that holds the <c>PatientManagementDb</c> section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPatientManagement(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PatientManagementDbOptions>()
            .Bind(configuration.GetSection(PatientManagementDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
