using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.Infrastructure;

/// <summary>The Infrastructure layer's single registration entry point.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Infrastructure layer provides: the OpenEMR identity provider and
    /// this service's token issuance. Hosts call this through the composition root.
    /// </summary>
    /// <param name="services">The service collection to add to. The <c>OpenEmr</c> and <c>Jwt</c> options must already be bound.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services
            .AddOpenEmrIdentityProvider()
            .AddJwtTokenIssuance();
    }
}
