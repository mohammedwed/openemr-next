using System.Net;
using System.Text.Json;
using Xunit;

namespace OpenEmrNext.AuthFacade.IntegrationTests;

/// <summary>
/// Live tests of <c>POST /connect/token</c> against the running stack. Each test checks a
/// status code and error body from a real OpenEMR response, so the expected values follow
/// OpenEMR's behavior. The unit tests cover the same paths with a fake identity provider.
/// </summary>
[Collection("Stack")]
[Trait("Category", "Integration")]
public class TokenEndpointTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    [Fact]
    public async Task Valid_credentials_return_an_AuthFacade_token()
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
}