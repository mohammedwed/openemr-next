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

    /// <summary>The base URL of the AuthFacade container on localhost. From <c>FACADE_HTTP_PORT</c>, default 7128.</summary>
    public static string AuthFacadeBaseUrl => $"http://localhost:{Get("FACADE_HTTP_PORT", "7128")}";

    /// <summary>The path of AuthFacade's token endpoint. From <c>AUTHFACADE_TOKEN_PATH</c>, default <c>/api/v1/connect/token</c>.</summary>
    public static string AuthFacadeTokenPath => Get("AUTHFACADE_TOKEN_PATH", "/api/v1/connect/token");

    /// <summary>The redirect URI registered for the AuthFacade OAuth client. Defaults to the AuthFacade base URL plus <c>/callback</c>.</summary>
    public static string AuthFacadeRedirectUri => Get("AUTHFACADE_REDIRECT_URI", $"{AuthFacadeBaseUrl}/callback");

    /// <summary>The path of AuthFacade's login page. From <c>AUTHFACADE_LOGIN_PATH</c>, default <c>/login</c>.</summary>
    public static string AuthFacadeLoginPath => Get("AUTHFACADE_LOGIN_PATH", "/login");

    /// <summary>The base URL of the OpenEMR container on localhost. From <c>WT_HTTPS_PORT</c>, default 9300.</summary>
    public static string OpenEmrBaseUrl => $"https://localhost:{Get("WT_HTTPS_PORT", "9300")}";

    /// <summary>The path of OpenEMR's OIDC discovery document. From <c>OPENEMR_DISCOVERY_PATH</c>.</summary>
    public static string OpenEmrDiscoveryPath => Get("OPENEMR_DISCOVERY_PATH", "/oauth2/default/.well-known/openid-configuration");

    /// <summary>The path of OpenEMR's OAuth token endpoint. From <c>OPENEMR_TOKEN_PATH</c>, default <c>/oauth2/default/token</c>.</summary>
    public static string OpenEmrTokenPath => Get("OPENEMR_TOKEN_PATH", "/oauth2/default/token");

    /// <summary>The OAuth client ID that AuthFacade uses with OpenEMR. From <c>AUTHFACADE_CLIENT_ID</c>. Empty if unset.</summary>
    public static string ClientId => Get("AUTHFACADE_CLIENT_ID");

    /// <summary>The OAuth client secret that AuthFacade uses with OpenEMR. From <c>AUTHFACADE_CLIENT_SECRET</c>. Empty if unset.</summary>
    public static string ClientSecret => Get("AUTHFACADE_CLIENT_SECRET");

    /// <summary>The OpenEMR account used for live password-grant tests. From <c>OE_USER</c>, default <c>admin</c>.</summary>
    public static string LoginUser => Get("OE_USER", "admin");

    /// <summary>The password for <see cref="LoginUser"/>. From <c>OE_PASS</c>. Empty if unset, so login tests fail.</summary>
    public static string LoginPassword => Get("OE_PASS");

    /// <summary>
    /// The MySQL connection string for the app database on localhost. Built from <c>WT_MYSQL_PORT</c>,
    /// <c>DB_NAME</c>, <c>DB_USER</c>, and <c>DB_PASSWORD</c>.
    /// </summary>
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