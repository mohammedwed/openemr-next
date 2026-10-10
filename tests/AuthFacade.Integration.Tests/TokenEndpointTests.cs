using System.Net;
using System.Text.Json;
using Xunit;

namespace AuthFacade.Integration.Tests;
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
    public async Task Wrong_password_is_rejected()
    {
        using var response = await PostPasswordGrantAsync(StackConfig.LoginUser, "definitely-wrong");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
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

    private static string TokenUrl => StackConfig.AuthFacadeBaseUrl + StackConfig.AuthFacadeTokenPath;

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