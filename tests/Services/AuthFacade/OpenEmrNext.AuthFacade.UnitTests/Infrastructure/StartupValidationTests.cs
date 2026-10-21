using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

public class StartupValidationTests
{
    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Jwt:Key"] = "unit-test-signing-key-at-least-32-bytes-long",
        ["OpenEmr:BaseUrl"] = "https://openemr.test",
        ["OpenEmr:ClientId"] = "facade-client",
        ["OpenEmr:ClientSecret"] = "facade-secret"
    };

    private static IHost BuildHost(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddServiceJwt(builder.Configuration);
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
