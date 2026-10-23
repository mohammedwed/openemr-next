using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;
using Polly.CircuitBreaker;

namespace OpenEmrNext.AuthFacade.UnitTests.Infrastructure;

/// <summary>
/// Tests the circuit breaker on the OpenEMR identity provider's client. They resolve the provider through
/// the real DI registration, with only the primary handler replaced by a fake, so the breaker is in the
/// request path.
/// </summary>
public class OpenEmrIdentityProviderCircuitBreakerTests
{
    /// <summary>
    /// The request count at which the failure ratio starts to apply. Every request in these tests fails,
    /// so reaching it opens the circuit.
    /// </summary>
    private const int MinimumThroughput = 5;

    private readonly FakeHttpMessageHandler _handler = new();
    private readonly IServiceProvider _services;

    public OpenEmrIdentityProviderCircuitBreakerTests()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton(Options.Create(new OpenEmrOptions
        {
            BaseUrl = "https://openemr.test",
            Issuer = "https://openemr.test/oauth2/default",
            ClientId = "facade-client",
            ClientSecret = "facade-secret"
        }));
        collection.AddOpenEmrIdentityProvider();
        collection.AddHttpClient<OpenEmrIdentityProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => _handler);

        _services = collection.BuildServiceProvider();
    }

    private IIdentityProvider Provider() => _services.GetRequiredService<IIdentityProvider>();

    private static Username Username() => new("alice_01");
    private static Password Password() => new("Passw0rd!");

    private void RespondWith(HttpStatusCode status, string body)
    {
        _handler.Exception = null;
        _handler.Response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private async Task SendAsync() =>
        await Provider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

    [Fact]
    public async Task Repeated_transport_failures_open_the_circuit_and_fail_fast()
    {
        _handler.Exception = new HttpRequestException("connection refused");

        for (var i = 0; i < MinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<IdentityProviderUnavailableException>(SendAsync);
        }

        // OpenEMR is healthy again, but the open circuit must still refuse the call.
        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"t\"}");

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(SendAsync);
        Assert.IsType<BrokenCircuitException>(ex.InnerException);
    }

    [Fact]
    public async Task Server_errors_count_as_failures_and_open_the_circuit()
    {
        for (var i = 0; i < MinimumThroughput; i++)
        {
            RespondWith(HttpStatusCode.InternalServerError, "{}");
            await SendAsync();
        }

        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"t\"}");

        var ex = await Assert.ThrowsAsync<IdentityProviderUnavailableException>(SendAsync);
        Assert.IsType<BrokenCircuitException>(ex.InnerException);
    }

    [Fact]
    public async Task Rejected_credentials_do_not_open_the_circuit()
    {
        for (var i = 0; i < MinimumThroughput * 2; i++)
        {
            RespondWith(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_grant\"}");
            var result = await Provider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);
            Assert.Equal(401, result.StatusCode);
        }

        RespondWith(HttpStatusCode.OK, "{\"access_token\":\"t\"}");
        var accepted = await Provider().PasswordGrantAsync(Username(), Password(), CancellationToken.None);

        Assert.Equal(200, accepted.StatusCode);
    }
}
