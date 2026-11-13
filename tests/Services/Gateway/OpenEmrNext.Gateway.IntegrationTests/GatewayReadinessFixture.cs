using System.Net;
using Xunit;

namespace OpenEmrNext.Gateway.IntegrationTests;

/// <summary>
/// Shared fixture for the <c>GatewayStack</c> collection. Before any integration test runs, it polls
/// one check until it passes or <see cref="GatewayStackConfig.ReadinessTimeout"/> expires: a request
/// routed through the gateway's HTTPS listener to AuthFacade's <c>/health</c> returns 200. This
/// transitively requires the gateway's listener, AuthFacade, OpenEMR, and the seed to already be
/// ready — the same dependency chain <c>AuthFacade.IntegrationTests.ReadinessFixture</c> waits on
/// directly, reached here through the one path these tests actually exercise.
/// </summary>
public sealed class GatewayReadinessFixture : IAsyncLifetime
{
    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        // Dev only: the gateway uses a self-signed certificate (ADR 0039).
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    public async Task InitializeAsync()
    {
        var deadline = DateTime.UtcNow + GatewayStackConfig.ReadinessTimeout;

        var checks = new (string Name, Func<Task<bool>> Check)[]
        {
            ("gateway routes /health to AuthFacade (200)", CheckAuthFacadeThroughGatewayAsync),
        };

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
                    $"Gateway stack not ready after {GatewayStackConfig.ReadinessTimeout.TotalMinutes:0} min. Still failing: {detail}");
            }

            await Task.Delay(Poll);
        }
    }

    /// <summary>No teardown. The stack is managed outside the test run.</summary>
    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<bool> CheckAuthFacadeThroughGatewayAsync()
    {
        var response = await Http.GetAsync(GatewayStackConfig.GatewayHttpsBaseUrl + "/health");
        return response.StatusCode == HttpStatusCode.OK;
    }
}

/// <summary>
/// Defines the <c>GatewayStack</c> xUnit collection. Tests that carry <c>[Collection("GatewayStack")]</c>
/// share one <see cref="GatewayReadinessFixture"/> and run one after another, not in parallel.
/// </summary>
[CollectionDefinition("GatewayStack")]
public class GatewayStackCollection : ICollectionFixture<GatewayReadinessFixture> { }
