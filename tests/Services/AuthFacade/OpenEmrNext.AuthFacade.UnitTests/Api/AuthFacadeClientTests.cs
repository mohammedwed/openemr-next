using System.Net.Http.Headers;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Client;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Proves the typed client NSwag generates from the committed OpenAPI snapshot
/// (OpenEmrNext.AuthFacade.Client/OpenAPIs/AuthFacade.json) actually works against the real pipeline — not
/// just that it compiles against the right shapes. A drift between the snapshot and the live document
/// would otherwise only surface once something downstream tried to use the generated client for real.
/// </summary>
public class AuthFacadeClientTests
{
    [Fact]
    public async Task Generated_client_logs_in_and_reads_identity_through_the_real_pipeline()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(200, new OpenEmrToken("openemr-token", "Bearer", 3600, "r-1"));
        api.TokenVerifier.Result = new VerifiedToken("subject-1", "Ann Example");
        var client = new AuthFacadeClient(api.Client);

        var token = await client.IssueTokenAsync(new Body
        {
            Grant_type = "password",
            Username = "alice_01",
            Password = "Passw0rd!"
        });

        Assert.Equal("openemr-token", token.Access_token);
        Assert.Equal("Bearer", token.Token_type);

        api.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Access_token);
        var me = await client.GetCurrentUserAsync();

        Assert.Equal("subject-1", me.Subject);
        Assert.Equal("Ann Example", me.Name);
    }

    [Fact]
    public async Task Generated_client_surfaces_a_refused_login_as_an_ApiException()
    {
        await using var api = await TestApi.StartAsync();
        api.IdentityProvider.Result = new PasswordGrantResult(400, null);
        var client = new AuthFacadeClient(api.Client);

        var exception = await Assert.ThrowsAsync<ApiException<ErrorResponse>>(() => client.IssueTokenAsync(new Body
        {
            Grant_type = "password",
            Username = "alice_01",
            Password = "wrong-password"
        }));

        Assert.Equal(400, exception.StatusCode);
    }
}
