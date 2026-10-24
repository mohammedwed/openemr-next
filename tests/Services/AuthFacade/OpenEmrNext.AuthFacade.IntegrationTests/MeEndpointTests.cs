using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace OpenEmrNext.AuthFacade.IntegrationTests;

/// <summary>
/// Live tests of <c>GET /api/v1/me</c> on the running stack. Tokens come from a real password grant
/// through <c>/api/v1/connect/token</c>, so these tests also depend on OpenEMR accepting
/// <see cref="StackConfig.LoginUser"/> and <see cref="StackConfig.LoginPassword"/>.
/// </summary>
[Collection("Stack")]
[Trait("Category", "Integration")]
public class MeEndpointTests
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static string MeUrl => StackConfig.AuthFacadeBaseUrl + "/api/v1/me";

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

    /// <summary>Sends <c>GET /api/v1/me</c> with the token as a Bearer credential.</summary>
    private static Task<HttpResponseMessage> GetMeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return Http.SendAsync(request);
    }

    /// <summary>Obtains a token through the live password grant. Fails the test if the grant does not succeed.</summary>
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

    [Fact]
    public async Task Me_rejects_a_malformed_token()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.token");

        using var response = await Http.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
