using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace AuthFacade.Integration.Tests;

[Collection("Stack")]
[Trait("Category", "Integration")]
public class MeEndpointTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static string MeUrl => StackConfig.AuthFacadeBaseUrl + "/me";

    [Fact]
    public async Task Me_rejects_requests_without_a_token()
    {
        using var response = await Http.GetAsync(MeUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_subject_of_the_token()
    {
        var token = await GetTokenAsync();
        var expectedSubject = new JwtSecurityTokenHandler().ReadJwtToken(token).Subject;

        using var response = await GetMeAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
Assert.Equal(expectedSubject, doc.RootElement.GetProperty("subject").GetString());
        Assert.False(string.IsNullOrWhiteSpace(expectedSubject));
    }

    [Fact]
    public async Task Me_rejects_a_tampered_token()
    {
        var token = await GetTokenAsync();
        var tampered = token[..^4] + "AAAA";

        using var response = await GetMeAsync(tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task<HttpResponseMessage> GetMeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return Http.SendAsync(request);
    }

    private static async Task<string> GetTokenAsync()
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = StackConfig.LoginUser,
            ["password"] = StackConfig.LoginPassword
        });

        using var response = await Http.PostAsync(
            StackConfig.AuthFacadeBaseUrl + StackConfig.AuthFacadeTokenPath, form);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}