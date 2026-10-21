using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Application.Authentication;

namespace OpenEmrNext.AuthFacade.Application;

/// <summary>Registers the application layer's services.</summary>
public static class DependencyInjection
{
    /// <summary>Registers <see cref="PasswordLoginHandler"/> as a scoped service.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PasswordLoginHandler>();
        return services;
    }
}
