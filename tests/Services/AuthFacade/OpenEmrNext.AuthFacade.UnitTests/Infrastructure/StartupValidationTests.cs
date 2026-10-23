using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Unit tests for the startup checks of <c>AddJwtAuthentication</c> and <c>AddOpenEmrIdentityProvider</c>.
/// Each test builds a real host and checks that required settings are enforced at start, not on
/// the first request.
/// </summary>
public class StartupValidationTests
{
    /// <summary>A complete, valid set of settings. Tests remove or change one key at a time.</summary>
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Jwt:Key"] = "unit-test-signing-key-at-least-32-bytes-long",
        ["Jwt:Issuer"] = "openemr-next",
        ["Jwt:Audience"] = "openemr-next-api",
        ["Jwt:LifetimeMinutes"] = "30",
        ["OpenEmr:BaseUrl"] = "https://openemr.test",
        ["OpenEmr:ClientId"] = "facade-client",
        ["OpenEmr:ClientSecret"] = "facade-secret"
    };

    /// <summary>Builds (but does not start) a host with the JWT and OpenEMR services registered from <paramref name="settings"/>.</summary>
    private static IHost BuildHost(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        // Drop the default sources, which include the appsettings.json copied from the API project,
        // so each test sees only the settings it sets.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddJwtAuthentication(builder.Configuration);
        builder.Services.AddOpenEmrIdentityProvider(builder.Configuration, builder.Environment);
        return builder.Build();
    }

    [Fact]
    public async Task Host_starts_when_all_required_settings_are_present()
    {
        using var host = BuildHost(ValidSettings());

        await host.StartAsync();
        await host.StopAsync();
    }

    [Theory]
    [InlineData("Jwt:Key")]
    [InlineData("Jwt:Issuer")]
    [InlineData("Jwt:Audience")]
    [InlineData("Jwt:LifetimeMinutes")]
    [InlineData("OpenEmr:BaseUrl")]
    [InlineData("OpenEmr:ClientId")]
    [InlineData("OpenEmr:ClientSecret")]
    public async Task Host_refuses_to_start_when_a_required_setting_is_missing(string missingKey)
    {
        var settings = ValidSettings();
        settings.Remove(missingKey);
        using var host = BuildHost(settings);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Host_refuses_to_start_when_the_jwt_lifetime_is_out_of_range()
    {
        var settings = ValidSettings();
        settings["Jwt:LifetimeMinutes"] = "0";
        using var host = BuildHost(settings);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }
}
