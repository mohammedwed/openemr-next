using System.Text.Json;
using MySqlConnector;
using Xunit;

namespace AuthFacade.Integration.Tests;

[Collection("Stack")]
[Trait("Category", "Integration")]
public class AuthFacadeStackTests
{
    // Dev only: the OpenEMR container uses a self-signed certificate.
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    [Fact]
    public async Task Seed_registered_the_AuthFacade_client()
    {
        await using var conn = new MySqlConnection(StackConfig.DbConnectionString);
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand(
            "SELECT is_enabled, scope, grant_types FROM oauth_clients WHERE client_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", StackConfig.ClientId);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "No AuthFacade client row. The seed did not run.");

        Assert.True(Convert.ToBoolean(reader["is_enabled"]));
        Assert.Equal("openid profile email offline_access", reader.GetString("scope"));
        Assert.Equal("authorization_code", reader.GetString("grant_types"));
    }

    [Fact]
    public async Task OpenEmr_publishes_its_OIDC_discovery_document()
    {
        var json = await Http.GetStringAsync(
            $"{StackConfig.OpenEmrBaseUrl}/oauth2/default/.well-known/openid-configuration");

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("token_endpoint", out _));
    }

    [Fact]
    public async Task OpenEmr_authenticates_the_AuthFacade_client_secret()
    {
        // A bogus authorization code should fail with invalid_grant if the client
        // authenticates. A wrong or missing secret fails earlier with invalid_client.
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = "invalid-test-code",
            ["redirect_uri"] = "http://localhost:7128/callback",
            ["client_id"] = StackConfig.ClientId,
            ["client_secret"] = StackConfig.ClientSecret
        });

        var response = await Http.PostAsync(
            $"{StackConfig.OpenEmrBaseUrl}/oauth2/default/token", form);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("invalid_client", body);
    }

    [Fact]
    public async Task AuthFacade_container_responds_over_HTTP()
    {
        var response = await Http.GetAsync(StackConfig.AuthFacadeBaseUrl + "/");

        // Any non-5xx response means the app is running. The exact status depends on its routes.
        Assert.True((int)response.StatusCode < 500,
            $"AuthFacade returned {(int)response.StatusCode}");
    }
}