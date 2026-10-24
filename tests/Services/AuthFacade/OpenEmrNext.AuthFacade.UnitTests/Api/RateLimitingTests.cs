using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Api.RateLimiting;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.UnitTests.Application;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Tests the login rate limit on <c>POST /api/v1/connect/token</c>. The default policy allows 10 requests a
/// minute per client address, and every test client shares one loopback address, so the first 10
/// requests in a test pass and the 11th is refused.
/// </summary>
public class RateLimitingTests
{
    /// <summary>Builds a password grant body that the fake identity provider rejects.</summary>
    private static FormUrlEncodedContent PasswordGrant() => new(new Dictionary<string, string>
    {
        ["grant_type"] = "password",
        ["username"] = "alice_01",
        ["password"] = "Wrong-Passw0rd!"
    });

    /// <summary>Starts the API host with the given rate limit settings and returns nothing. Used by startup checks.</summary>
    private static async Task StartHostAsync(int permitLimit, TimeSpan window)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenEmr:BaseUrl"] = "https://openemr.test",
            ["OpenEmr:Issuer"] = "https://openemr.test/oauth2/default",
            ["OpenEmr:ClientId"] = "facade-client",
            ["OpenEmr:ClientSecret"] = "facade-secret",
            ["LoginRateLimit:PermitLimit"] = permitLimit.ToString(),
            ["LoginRateLimit:Window"] = window.ToString()
        });

        builder.AddAuthFacadeHttpDependencies();

        await using var app = builder.Build();
        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task Requests_over_the_login_limit_get_429_with_retry_after()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(400, null);

        for (var i = 0; i < 10; i++)
        {
            using var attempt = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant());
            Assert.NotEqual(HttpStatusCode.TooManyRequests, attempt.StatusCode);
        }

        using var refused = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant());

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.NotNull(refused.Headers.RetryAfter);
        using var body = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        Assert.Equal("too_many_requests", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Refused_login_under_the_limit_is_still_an_invalid_grant()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(400, null);

        using var response = await api.Client.PostAsync("/api/v1/connect/token", PasswordGrant());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Me_is_not_rate_limited_by_the_login_policy()
    {
        await using var api = await TestApi.StartAsync();
        api.TokenVerifier.Result = new VerifiedToken("subject-1", null);

        for (var i = 0; i < 15; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "openemr-token");
            using var me = await api.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
    }

    [Fact]
    public async Task Host_refuses_to_start_with_a_permit_limit_below_one()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync(permitLimit: 0, window: TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Host_refuses_to_start_with_a_window_under_one_second()
    {
        await Assert.ThrowsAsync<OptionsValidationException>(() => StartHostAsync(permitLimit: 10, window: TimeSpan.FromMilliseconds(500)));
    }
}
