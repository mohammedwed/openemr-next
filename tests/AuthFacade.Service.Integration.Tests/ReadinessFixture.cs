using System.Net;
using MySqlConnector;
using Xunit;

namespace AuthFacade.Integration.Tests;

public sealed class ReadinessFixture : IAsyncLifetime
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        // Dev only: the OpenEMR container uses a self-signed certificate
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(15);

    public async Task InitializeAsync()
    {
        var deadline = DateTime.UtcNow + StackConfig.ReadinessTimeout;

        var checks = new (string Name, Func<Task<bool>> Check)[]
        {
            ("database accepts the app user", CheckDatabaseAsync),
            ("AuthFacade client is registered", CheckSeedAsync),
            ("OpenEMR readiness endpoint returns 200", CheckOpenEmrAsync),
            ("AuthFacade responds on /hello", CheckAuthFacadeAsync),
        };

        // Poll all checks together until they pass or the single deadline passes
        var pending = checks.ToList();
        var lastResults = new Dictionary<string, string>();

        while (pending.Count > 0)
        {
            var results = await Task.WhenAll(pending.Select(async c =>
            {
                try
                {
                    return (c.Name, Passed: await c.Check(), Error: (string?)null);
                }
                catch (MySqlException ex) when (ex.Number == 1045)
                {
                    // Access denied: the config is wrong and will not fix itself
                    throw new InvalidOperationException(
                        $"Database rejected the app user ({c.Name}). Check DB_PASSWORD in .env matches the volume. {ex.Message}");
                }
                catch (Exception ex)
                {
                    return (c.Name, Passed: false, Error: ex.Message);
                }
            }));

            foreach (var r in results)
            {
                if (r.Passed) pending.RemoveAll(p => p.Name == r.Name);
                else lastResults[r.Name] = r.Error ?? "check returned false";
            }

            if (pending.Count == 0) return;

            if (DateTime.UtcNow >= deadline)
            {
                var detail = string.Join("; ", pending.Select(p => $"{p.Name}: {lastResults.GetValueOrDefault(p.Name, "no result")}"));
                throw new InvalidOperationException(
                    $"Stack not ready after {StackConfig.ReadinessTimeout.TotalMinutes:0} min. Still failing: {detail}");
            }

            await Task.Delay(Poll);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<bool> CheckDatabaseAsync()
    {
        await using var conn = new MySqlConnection(StackConfig.DbConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT 1", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> CheckSeedAsync()
    {
        await using var conn = new MySqlConnection(StackConfig.DbConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM oauth_clients WHERE client_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", StackConfig.ClientId);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> CheckOpenEmrAsync()
    {
        var response = await Http.GetAsync(StackConfig.OpenEmrBaseUrl + "/meta/health/readyz");
        return response.StatusCode == HttpStatusCode.OK;
    }

    private static async Task<bool> CheckAuthFacadeAsync()
    {
        var response = await Http.GetAsync(StackConfig.AuthFacadeBaseUrl + "/hello");
        return response.StatusCode == HttpStatusCode.OK;
    }
}

[CollectionDefinition("Stack")]
public class StackCollection : ICollectionFixture<ReadinessFixture> { }