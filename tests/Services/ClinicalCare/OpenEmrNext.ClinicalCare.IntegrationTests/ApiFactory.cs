using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Hosts the real API in-process against the container, configured exactly as the deployed service is: the
/// production principal's credentials and a shared secret (QLT-9, QLT-8).
/// </summary>
internal static class ApiFactory
{
    public const string Secret = "test-secret";
    public const string SecretHeader = "X-Internal-Gateway-Secret";
    public const string EventsSecret = "test-events-secret";
    public const string EventsSecretHeader = "X-Internal-Events-Secret";

    public static WebApplicationFactory<Program> Create(
        ClinicalCareContainerFixture fixture, Action<IWebHostBuilder>? configure = null, string? secret = Secret,
        string? eventsSecret = EventsSecret)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new List<KeyValuePair<string, string?>>
                {
                    new("ClinicalCareDb:Host", fixture.Hostname),
                    new("ClinicalCareDb:Port", fixture.Port.ToString()),
                    new("ClinicalCareDb:Database", fixture.Database),
                    new("ClinicalCareDb:User", ClinicalCareContainerFixture.User),
                    new("ClinicalCareDb:Password", ClinicalCareContainerFixture.DbPassword)
                };
                if (secret is not null)
                    values.Add(new("InternalGateway:Secret", secret));
                if (eventsSecret is not null)
                    values.Add(new("InternalEvents:Secret", eventsSecret));
                config.AddInMemoryCollection(values);
            });
            configure?.Invoke(builder);
        });
    }

    public static HttpClient ClientWithSecret(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(SecretHeader, Secret);
        return client;
    }
}
