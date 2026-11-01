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

    /// <summary>The base URL of the AuthFacade container on localhost. From <c>AUTHFACADE_HTTP_PORT</c>, default 7128.</summary>
    public static string AuthFacadeBaseUrl => $"http://localhost:{Get("AUTHFACADE_HTTP_PORT", "7128")}";

    /// <summary>The base URL of the gateway on localhost. From <c>GATEWAY_HTTP_PORT</c>, default 7130.</summary>
    public static string GatewayBaseUrl => $"http://localhost:{Get("GATEWAY_HTTP_PORT", "7130")}";

    /// <summary>
    /// The redirect URI registered for the AuthFacade OAuth client. Defaults to the gateway's base URL plus
    /// <c>/callback</c> — the seed registers the gateway's address, not AuthFacade's own (ADR 0033), and
    /// AuthFacade itself sends this same value when it builds the authorize URL.
    /// </summary>
    public static string AuthFacadeRedirectUri => Get("AUTHFACADE_REDIRECT_URI", $"{GatewayBaseUrl}/callback");

    /// <summary>The base URL of the OpenEMR container on localhost. From <c>OPENEMR_HTTPS_PORT</c>, default 9300.</summary>
    public static string OpenEmrBaseUrl => $"https://localhost:{Get("OPENEMR_HTTPS_PORT", "9300")}";

    /// <summary>The path of OpenEMR's OIDC discovery document. From <c>OPENEMR_DISCOVERY_PATH</c>.</summary>
    public static string OpenEmrDiscoveryPath => Get("OPENEMR_DISCOVERY_PATH", "/oauth2/default/.well-known/openid-configuration");

    /// <summary>The path of OpenEMR's OAuth authorize endpoint. From <c>OPENEMR_AUTHORIZE_PATH</c>, default <c>/oauth2/default/authorize</c>.</summary>
    public static string OpenEmrAuthorizePath => Get("OPENEMR_AUTHORIZE_PATH", "/oauth2/default/authorize");

    /// <summary>The path of OpenEMR's OAuth token endpoint. From <c>OPENEMR_TOKEN_PATH</c>, default <c>/oauth2/default/token</c>.</summary>
    public static string OpenEmrTokenPath => Get("OPENEMR_TOKEN_PATH", "/oauth2/default/token");

    /// <summary>The OAuth client ID that AuthFacade uses with OpenEMR. From <c>AUTHFACADE_CLIENT_ID</c>. Empty if unset.</summary>
    public static string ClientId => Get("AUTHFACADE_CLIENT_ID");

    /// <summary>The OAuth client secret that AuthFacade uses with OpenEMR. From <c>AUTHFACADE_CLIENT_SECRET</c>. Empty if unset.</summary>
    public static string ClientSecret => Get("AUTHFACADE_CLIENT_SECRET");

    /// <summary>
    /// The MariaDB connection string for the app database on localhost. Built from <c>DB_PORT</c>,
    /// <c>DB_NAME</c>, <c>DB_USER</c>, and <c>DB_PASSWORD</c>.
    /// </summary>
    public static string DbConnectionString =>
        $"Server=localhost;Port={Get("DB_PORT", "8320")};Database={Get("DB_NAME", "openemr")};" +
        $"User={Get("DB_USER", "openemr")};Password={Get("DB_PASSWORD")};CharSet=utf8mb4;";

    /// <summary>
    /// Reads the nearest <c>.env</c> file, searching up from the test binary's directory. Blank
    /// lines, comments, and lines without <c>=</c> are skipped, and surrounding double quotes are removed from values. Returns an empty dictionary if no
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
            values[parts[0].Trim()] = parts[1].Trim().Trim('"');
        }
        return values;
    }
}