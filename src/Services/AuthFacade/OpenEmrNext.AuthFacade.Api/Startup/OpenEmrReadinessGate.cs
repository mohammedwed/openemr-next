using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.Api.Startup;

/// <summary>
/// Waits for OpenEMR's own readiness probe to answer before the app starts serving, closing the window
/// where an early request would otherwise fail with a raw connection exception while OpenEMR is still
/// booting. Non-fatal: Compose's own <c>depends_on: service_healthy</c> already keeps this check's
/// common case to one successful call, so a timeout here only matters outside Compose (a bare
/// <c>dotnet run</c>, or a race); when it happens, this logs a warning and lets the app start anyway — a
/// request that then fails fails no worse than it would have without this check, and a host that never
/// starts is worse than one that started slow.
/// </summary>
public static class OpenEmrReadinessGate
{
    internal const string ReadyzPath = "/meta/health/readyz";
    internal static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <summary>Polls <c>OpenEmr:BaseUrl</c>'s readiness probe, using the thresholds above.</summary>
    /// <param name="app">The built application, used to resolve the bound <see cref="OpenEmrOptions"/> and a logger.</param>
    public static async Task WaitForOpenEmrAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<OpenEmrOptions>>().Value;
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OpenEmrReadinessGate));
        var readyzUri = new Uri(options.BaseUrl.TrimEnd('/') + ReadyzPath);

        using var http = new HttpClient();
        await PollUntilReadyAsync(http, readyzUri, MaxWait, PollInterval, logger);
    }

    /// <summary>
    /// Polls <paramref name="readyzUri"/> every <paramref name="pollInterval"/> until it answers
    /// successfully or <paramref name="maxWait"/> elapses. Never throws: a request-level failure, a
    /// timeout, or the overall deadline all just mean "not ready yet," not an error.
    /// </summary>
    internal static async Task PollUntilReadyAsync(
        HttpClient http, Uri readyzUri, TimeSpan maxWait, TimeSpan pollInterval, ILogger logger)
    {
        var deadline = DateTime.UtcNow + maxWait;

        while (true)
        {
            if (await IsReadyAsync(http, readyzUri, pollInterval))
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                logger.LogWarning(
                    "OpenEMR at {ReadyzUri} did not report ready within {MaxWait}; starting anyway.",
                    readyzUri, maxWait);
                return;
            }

            await Task.Delay(pollInterval);
        }
    }

    private static async Task<bool> IsReadyAsync(HttpClient http, Uri readyzUri, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            using var response = await http.GetAsync(readyzUri, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return false;
        }
    }
}
