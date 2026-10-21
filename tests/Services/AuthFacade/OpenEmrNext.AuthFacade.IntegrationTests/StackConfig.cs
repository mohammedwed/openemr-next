namespace OpenEmrNext.AuthFacade.IntegrationTests;

/// <summary>
/// Settings for the live integration tests. Each value is read from an environment variable
/// first, then from the repository's <c>.env</c> file, then from a default that matches the
/// Docker Compose stack.
/// </summary>
public static class StackConfig
{
    private static readonly Dictionary<string, string> Env = LoadEnvFile();

    /// <summary>How long <see cref="ReadinessFixture"/> waits for the stack. From <c>STACK_READY_TIMEOUT_MINUTES</c>, default 3.</summary>
    public static TimeSpan ReadinessTimeout =>
        TimeSpan.FromMinutes(int.Parse(Get("STACK_READY_TIMEOUT_MINUTES", "3")));

    /// <summary>Returns the value for <paramref name="key"/>: environment variable, then <c>.env</c>, then <paramref name="fallback"/>.</summary>
    private static string Get(string key, string fallback = "") =>
        Environment.GetEnvironmentVariable(key)
        ?? (Env.TryGetValue(key, out var value) ? value : fallback);

    public static string AuthFacadeBaseUrl => $"http://localhost:{Get("FACADE_HTTP_PORT", "7128")}";
    public static string AuthFacadeTokenPath => Get("AUTHFACADE_TOKEN_PATH", "/connect/token");
    public static string AuthFacadeRedirectUri => Get("AUTHFACADE_REDIRECT_URI", $"{AuthFacadeBaseUrl}/callback");
    public static string AuthFacadeLoginPath => Get("AUTHFACADE_LOGIN_PATH", "/login");

    public static string OpenEmrBaseUrl => $"https://localhost:{Get("WT_HTTPS_PORT", "9300")}";
    public static string OpenEmrDiscoveryPath => Get("OPENEMR_DISCOVERY_PATH", "/oauth2/default/.well-known/openid-configuration");
    public static string OpenEmrTokenPath => Get("OPENEMR_TOKEN_PATH", "/oauth2/default/token");

    public static string ClientId => Get("AUTHFACADE_CLIENT_ID");
    public static string ClientSecret => Get("AUTHFACADE_CLIENT_SECRET");

    public static string LoginUser => Get("OE_USER", "admin");
    public static string LoginPassword => Get("OE_PASS");

    public static string DbConnectionString =>
        $"Server=localhost;Port={Get("WT_MYSQL_PORT", "8320")};Database={Get("DB_NAME", "openemr")};" +
        $"User={Get("DB_USER", "openemr")};Password={Get("DB_PASSWORD")};CharSet=utf8mb4;";

    /// <summary>
    /// Reads the nearest <c>.env</c> file, searching up from the test binary's directory. Blank
    /// lines, comments, and lines without <c>=</c> are skipped. Returns an empty dictionary if no
    /// file is found.
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
            values[parts[0].Trim()] = parts[1].Trim();
        }
        return values;
    }
}