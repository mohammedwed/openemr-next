using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.UnitTests.Application;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Runs the real AuthFacade pipeline on a random local port with the OpenEMR identity
/// provider replaced by a fake. Needs no Docker stack and no extra test packages.
/// </summary>
internal sealed class TestApi : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApi(WebApplication app, FakeOpenEmrIdentityProvider identityProvider)
    {
        _app = app;
        IdentityProvider = identityProvider;
        Client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
    }

    public FakeOpenEmrIdentityProvider IdentityProvider { get; }

    public HttpClient Client { get; }

    public static async Task<TestApi> StartAsync()
    {
        var identityProvider = new FakeOpenEmrIdentityProvider();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "unit-test-signing-key-at-least-32-bytes-long",
            ["OpenEmr:BaseUrl"] = "https://openemr.test",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret"
        });

        builder.AddAuthFacadeApi();
        // Registered last, so it replaces the real OpenEMR client.
        builder.Services.AddSingleton<IOpenEmrIdentityProvider>(identityProvider);

        var app = builder.Build();
        app.UseAuthFacadeApi();
        await app.StartAsync();

        return new TestApi(app, identityProvider);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
