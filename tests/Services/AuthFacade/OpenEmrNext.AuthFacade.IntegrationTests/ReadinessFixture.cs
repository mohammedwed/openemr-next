using System.Net;
using MySqlConnector;
using Xunit;

namespace OpenEmrNext.AuthFacade.IntegrationTests;

/// <summary>
/// Shared fixture for the <c>Stack</c> collection. Before any integration test runs, it polls
/// four checks until all pass or <see cref="StackConfig.ReadinessTimeout"/> expires:
/// the database accepts the app user, the AuthFacade client row is seeded, OpenEMR's
/// <c>/meta/health/readyz</c> returns 200, and AuthFacade's <c>/api/health</c> returns 200.
/// </summary>
/// <remarks>
/// Each check has its own timeout. A MySQL access-denied error (1045) fails fast instead of
/// retrying, because a wrong <c>DB_PASSWORD</c> does not fix itself.
/// </remarks>
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

    // Upper bound for any single check, so one hung request can't stall the round
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Waits for the stack to become ready. Runs before the first test in the <c>Stack</c> collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The database rejects the app user, or the checks still fail when <see cref="StackConfig.ReadinessTimeout"/> expires.
    /// </exception>
    public async Task InitializeAsync()
    {
        var deadline = DateTime.UtcNow + StackConfig.ReadinessTimeout;

        var checks = new (string Name, Func<Task<bool>> Check)[]
        {
            ("database accepts the app user", CheckDatabaseAsync),
            ("AuthFacade client is registered", CheckSeedAsync),
            ("OpenEMR readiness endpoint returns 200", CheckOpenEmrAsync),
            ("AuthFacade responds on /api/health", CheckAuthFacadeAsync),
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
                    var passed = await c.Check().WaitAsync(CheckTimeout);
                    return (c.Name, Passed: passed, Error: (string?)null);
                }
                catch (MySqlException ex) when (ex.Number == 1045)
                {
                    // Access denied: the config is wrong and will not fix itself
                    throw new InvalidOperationException(
                        $"Database rejected the app user ({c.Name}). Check DB_PASSWORD in .env matches the volume. {ex.Message}");
                }
                catch (Exception ex)
                {
                    return (c.Name, Passed: false, Error: $"{ex.GetType().Name}: {ex.Message}");
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

    /// <summary>No teardown. The stack is managed outside the test run.</summary>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Runs <c>SELECT 1</c> against the app database.</summary>
    private static async Task<bool> CheckDatabaseAsync()
    {
        await using var conn = new MySqlConnection(StackConfig.DbConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT 1", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) == 1;
    }

    /// <summary>Checks that exactly one <c>oauth_clients</c> row exists for the AuthFacade client ID.</summary>
    private static async Task<bool> CheckSeedAsync()
    {
        await using var conn = new MySqlConnection(StackConfig.DbConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            "SELECT COUNT(*) FROM oauth_clients WHERE client_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", StackConfig.ClientId);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) == 1;
    }

    /// <summary>Checks that OpenEMR's readiness endpoint returns 200.</summary>
    private static async Task<bool> CheckOpenEmrAsync()
    {
        var response = await Http.GetAsync(StackConfig.OpenEmrBaseUrl + "/meta/health/readyz");
        return response.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>Checks that AuthFacade's <c>/api/health</c> endpoint returns 200.</summary>
    private static async Task<bool> CheckAuthFacadeAsync()
    {
        var response = await Http.GetAsync(StackConfig.AuthFacadeBaseUrl + "/api/health");
        return response.StatusCode == HttpStatusCode.OK;
    }
}

/// <summary>
/// Defines the <c>Stack</c> xUnit collection. Tests that carry <c>[Collection("Stack")]</c> share
/// one <see cref="ReadinessFixture"/> and run one after another, not in parallel.
/// </summary>
[CollectionDefinition("Stack")]
public class StackCollection : ICollectionFixture<ReadinessFixture> { }