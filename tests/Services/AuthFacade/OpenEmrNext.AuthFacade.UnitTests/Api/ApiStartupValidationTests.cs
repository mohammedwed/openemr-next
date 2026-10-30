using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Checks the startup rule the API host adds: <c>OpenEmr:AcceptAnyServerCertificate</c> may be
/// <c>true</c> only in the Development environment. Each test starts a real host and checks whether
/// startup succeeds.
/// </summary>
public class ApiStartupValidationTests
{
    /// <summary>Starts the API host in the given environment with the given certificate setting.</summary>
    private static async Task StartHostAsync(string environment, bool acceptAnyCertificate)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = environment
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = "https://openemr.test",
            ["OpenEmr:Issuer"] = "https://openemr.test/oauth2/default",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret",
            ["OpenEmr:RedirectUri"] = "https://facade.test/callback",
            ["OpenEmr:AcceptAnyServerCertificate"] = acceptAnyCertificate.ToString(),
            ["BrowserFlow:DefaultReturnUrl"] = "https://app.test/",
            ["BrowserFlow:ReturnUrlAllowedHosts:0"] = "app.test"
        });

        builder.AddAuthFacadeHttpDependencies();

        await using var app = builder.Build();
        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task Certificate_bypass_is_refused_outside_development()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync("Production", acceptAnyCertificate: true));
    }

    [Fact]
    public async Task Host_starts_outside_development_when_the_bypass_is_off()
    {
        await StartHostAsync("Production", acceptAnyCertificate: false);
    }

    [Fact]
    public async Task Certificate_bypass_is_allowed_in_development()
    {
        await StartHostAsync("Development", acceptAnyCertificate: true);
    }
}
