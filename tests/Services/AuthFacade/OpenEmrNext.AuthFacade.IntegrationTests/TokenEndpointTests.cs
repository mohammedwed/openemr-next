using System.Net;
using System.Text.Json;
using Xunit;

namespace OpenEmrNext.AuthFacade.IntegrationTests;

/// <summary>
/// Live tests of <c>POST /api/v1/connect/token</c> against the running stack. Each test checks a
/// status code and body from a real OpenEMR response, so the expected values follow
/// OpenEMR's behavior. Successful bodies are AuthFacade's token response, built from OpenEMR's verified token. The unit tests cover the same paths with a fake identity provider.
/// </summary>
[Collection("Stack")]
[Trait("Category", "Integration")]
public class TokenEndpointTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    [Fact]
    public async Task Valid_credentials_return_the_openemr_token_response()
    {
        using var response = await PostPasswordGrantAsync(StackConfig.LoginUser, StackConfig.LoginPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", doc.RootElement.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task Wrong_password_passes_through_openemr_rejection()
    {
        using var response = await PostPasswordGrantAsync(StackConfig.LoginUser, "definitely-wrong");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Short_legacy_username_is_decided_by_openemr_not_locally()
    {
        using var response = await PostPasswordGrantAsync("a", "x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Oversized_password_is_rejected_before_reaching_openemr()
    {
        using var response = await PostPasswordGrantAsync(StackConfig.LoginUser, new string('x', 1025));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Unsupported_grant_type_is_rejected()
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["username"] = StackConfig.LoginUser,
            ["password"] = StackConfig.LoginPassword
        });

        using var response = await Http.PostAsync(TokenUrl, form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Rejecting an invalid or missing refresh token is unit-tested (TokenRequestTests, TokenGrantHandlerTests)
    // and shares the same OpenEMR-rejection and local-validation code paths the password-grant tests above
    // already exercise live. Only the happy path gets its own live test here: OpenEMR actually accepting a
    // real refresh token end to end is the one fact no unit test can simulate. Each added live case also
    // counts against the login rate limit this whole file shares (ADR 0018), so new cases here should earn
    // their own real HTTP round trip, not duplicate what a fake identity provider already proves.
    [Fact]
    public async Task Refresh_token_from_a_password_grant_redeems_for_a_new_access_token()
    {
        using var loginResponse = await PostPasswordGrantAsync(StackConfig.LoginUser, StackConfig.LoginPassword);
        using var loginDoc = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var refreshToken = loginDoc.RootElement.GetProperty("refresh_token").GetString()!;

        using var response = await PostRefreshGrantAsync(refreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("access_token").GetString()));
    }

    /// <summary>The full URL of the AuthFacade token endpoint on the running stack.</summary>
    private static string TokenUrl => StackConfig.AuthFacadeBaseUrl + StackConfig.AuthFacadeTokenPath;

    /// <summary>Posts a form-encoded password grant to the token endpoint and returns the raw response.</summary>
    private static async Task<HttpResponseMessage> PostPasswordGrantAsync(string username, string password)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        });

        return await Http.PostAsync(TokenUrl, form);
    }

    /// <summary>Posts a form-encoded refresh grant to the token endpoint and returns the raw response.</summary>
    private static async Task<HttpResponseMessage> PostRefreshGrantAsync(string refreshToken)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        return await Http.PostAsync(TokenUrl, form);
    }
}
