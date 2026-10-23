using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace OpenEmrNext.AuthFacade.IntegrationTests;

/// <summary>
/// Live tests of the RFC 8693 token exchange. The OpenEMR token comes from OpenEMR's own password
/// grant, so the exchange is checked against OpenEMR's real signing keys and claims.
/// </summary>
[Collection("Stack")]
[Trait("Category", "Integration")]
public class TokenExchangeTests
{
    private const string TokenExchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string JwtTokenType = "urn:ietf:params:oauth:token-type:jwt";

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        // Dev only: the OpenEMR container uses a self-signed certificate
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    [Fact]
    public async Task Openemr_token_is_exchanged_for_a_token_that_identifies_the_same_subject()
    {
        var openEmrToken = await GetOpenEmrTokenAsync();

        using var exchange = await ExchangeAsync(openEmrToken);

        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var exchanged = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        var facadeToken = exchanged.RootElement.GetProperty("access_token").GetString()!;

        using var me = await GetMeAsync(facadeToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var profile = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(SubjectOf(openEmrToken), profile.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Openemr_token_with_an_altered_signature_is_rejected()
    {
        var openEmrToken = await GetOpenEmrTokenAsync();
        var last = openEmrToken[^1];
        var altered = openEmrToken[..^1] + (last == 'A' ? 'B' : 'A');

        using var exchange = await ExchangeAsync(altered);

        Assert.Equal(HttpStatusCode.BadRequest, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
    }

    /// <summary>Gets an access token from OpenEMR directly, the way the legacy system issues one.</summary>
    private static async Task<string> GetOpenEmrTokenAsync()
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = StackConfig.ClientId,
            ["client_secret"] = StackConfig.ClientSecret,
            ["user_role"] = "users",
            ["username"] = StackConfig.LoginUser,
            ["password"] = StackConfig.LoginPassword,
            ["scope"] = "openid api:oemr"
        });

        using var response = await Http.PostAsync(StackConfig.OpenEmrBaseUrl + StackConfig.OpenEmrTokenPath, form);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    /// <summary>Posts a token exchange to AuthFacade's token endpoint.</summary>
    private static Task<HttpResponseMessage> ExchangeAsync(string subjectToken) =>
        Http.PostAsync(StackConfig.AuthFacadeBaseUrl + StackConfig.AuthFacadeTokenPath, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = TokenExchangeGrant,
                ["subject_token"] = subjectToken,
                ["subject_token_type"] = JwtTokenType
            }));

    /// <summary>Calls AuthFacade's <c>/me</c> with a bearer token.</summary>
    private static Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, StackConfig.AuthFacadeBaseUrl + "/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return Http.SendAsync(request);
    }

    /// <summary>Reads the <c>sub</c> claim from a JWT's payload. The signature is not checked; the test only needs the value.</summary>
    private static string SubjectOf(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        return doc.RootElement.GetProperty("sub").GetString()!;
    }
}
