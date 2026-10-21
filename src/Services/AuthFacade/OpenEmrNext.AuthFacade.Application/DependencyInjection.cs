using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Application.Authentication;

namespace OpenEmrNext.AuthFacade.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PasswordLoginHandler>();
        return services;
    }
}
