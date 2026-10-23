using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.Infrastructure;

/// <summary>The Infrastructure layer's single registration entry point.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Infrastructure layer provides: the OpenEMR identity provider and
    /// the verifier for OpenEMR-issued tokens. Hosts call this through the composition root.
    /// </summary>
    /// <param name="services">The service collection to add to. The <c>OpenEmr</c> options must already be bound.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services
            .AddOpenEmrIdentityProvider()
            .AddOpenEmrTokenVerifier();
    }
}
