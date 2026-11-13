namespace OpenEmrNext.Gateway.IntegrationTests;

/// <summary>
/// Settings for the live integration tests. Each value is read from an environment variable
/// first, then from the repository's <c>.env</c> file, then from a default that matches the
/// Docker Compose stack. Mirrors <c>OpenEmrNext.AuthFacade.IntegrationTests.StackConfig</c>,
/// scoped to just what these gateway-routing tests need.
/// </summary>
public static class GatewayStackConfig
{
    private static readonly Dictionary<string, string> Env = LoadEnvFile();

    /// <summary>How long <see cref="GatewayReadinessFixture"/> waits for the stack. From <c>STACK_READY_TIMEOUT_MINUTES</c>, default 3.</summary>
    public static TimeSpan ReadinessTimeout =>
        TimeSpan.FromMinutes(int.Parse(Get("STACK_READY_TIMEOUT_MINUTES", "3")));

    /// <summary>Returns the value for <paramref name="key"/>: environment variable, then <c>.env</c>, then <paramref name="fallback"/>.</summary>
    private static string Get(string key, string fallback = "") =>
        Environment.GetEnvironmentVariable(key)
        ?? (Env.TryGetValue(key, out var value) ? value : fallback);

    /// <summary>The base URL of the gateway's plain-HTTP listener on localhost. From <c>GATEWAY_HTTP_PORT</c>, default 7130. Redirects to <see cref="GatewayHttpsBaseUrl"/> and serves nothing else (ADR 0039).</summary>
    public static string GatewayHttpBaseUrl => $"http://localhost:{Get("GATEWAY_HTTP_PORT", "7130")}";

    /// <summary>The base URL of the gateway's HTTPS listener on localhost. From <c>GATEWAY_HTTPS_PORT</c>, default 7131 (ADR 0039).</summary>
    public static string GatewayHttpsBaseUrl => $"https://localhost:{Get("GATEWAY_HTTPS_PORT", "7131")}";

    /// <summary>
    /// Reads the nearest <c>.env</c> file, searching up from the test binary's directory. Blank
    /// lines, comments, and lines without <c>=</c> are skipped, and surrounding double quotes are
    /// removed from values. Returns an empty dictionary if no file is found.
    /// </summary>
    private static Dictionary<string, string> LoadEnvFile()
    {
        var values = new Dictionary<string, string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, ".env")))
            dir = dir.Parent;
        if (dir is null) return values;

        foreach (var line in File.ReadLines(Path.Combine(dir.FullName, ".env")))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || !trimmed.Contains('=')) continue;
            var parts = trimmed.Split('=', 2);
            values[parts[0].Trim()] = parts[1].Trim().Trim('"');
        }
        return values;
    }
}
