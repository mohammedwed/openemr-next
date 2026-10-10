namespace AuthFacade.Services;

public sealed class OpenEmrAuthClient(HttpClient http, IConfiguration config)
{
    private readonly string _clientId = config["OpenEmr:ClientId"]
        ?? throw new InvalidOperationException("Missing 'OpenEmr:ClientId'.");
    private readonly string _clientSecret = config["OpenEmr:ClientSecret"]
        ?? throw new InvalidOperationException("Missing 'OpenEmr:ClientSecret'.");
    private readonly string _scope = config["OpenEmr:Scope"] ?? "openid api:oemr";

    // Forwards the password grant to OpenEMR and returns its status and body unchanged.
    // Credentials are never logged.
    public async Task<(int Status, string Body)> PasswordGrantAsync(
        string username, string password, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["grant_type"] = "password",
    ["client_id"] = _clientId,
    ["client_secret"] = _clientSecret,
    ["user_role"] = "users",
    ["username"] = username,
    ["password"] = password,
    ["scope"] = _scope
});

        using var response = await http.PostAsync("oauth2/default/token", form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return ((int)response.StatusCode, body);
    }
}