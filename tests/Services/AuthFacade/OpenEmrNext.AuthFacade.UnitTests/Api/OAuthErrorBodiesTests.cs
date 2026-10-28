using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenEmrNext.AuthFacade.Api.Endpoints;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the pairing of <c>AddOAuthErrorBodies</c> and <c>UseOAuthErrorBodies</c>. In Development ASP.NET Core
/// throws on a binding failure by default, so using the middleware without the service setting must fail at startup.
/// </summary>
public class OAuthErrorBodiesTests
{
    private static WebApplication BuildApp(bool addErrorBodies)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        if (addErrorBodies) builder.Services.AddOAuthErrorBodies();
        return builder.Build();
    }

    [Fact]
    public void Using_the_middleware_without_the_service_setting_fails_at_startup()
    {
        var app = BuildApp(addErrorBodies: false);

        var error = Assert.Throws<InvalidOperationException>(() => app.UseOAuthErrorBodies());

        Assert.Contains(nameof(OAuthErrorBodies.AddOAuthErrorBodies), error.Message);
    }

    [Fact]
    public void Using_the_middleware_after_the_service_setting_succeeds()
    {
        var app = BuildApp(addErrorBodies: true);

        app.UseOAuthErrorBodies();
    }
}
