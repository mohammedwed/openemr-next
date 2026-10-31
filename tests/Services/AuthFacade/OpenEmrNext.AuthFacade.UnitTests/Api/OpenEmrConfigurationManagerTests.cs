using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OpenEmrNext.AuthFacade.Api.OpenIddictClient;
using OpenEmrNext.AuthFacade.UnitTests.Infrastructure;
using OpenIddict.Abstractions;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="OpenEmrConfigurationManager"/>. These guard the two real bugs found building it
/// (ADR 0026): <c>JsonWebKeySet</c> alone is not enough for token validation — <c>SigningKeys</c> must be
/// populated too — and the fetch must be cached, not repeated on every call.
/// </summary>
public class OpenEmrConfigurationManagerTests
{
    private static readonly Uri JwksUri = new("https://openemr.test/oauth2/default/jwk");

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly FakeHttpMessageHandler _handler = new();

    /// <summary>
    /// Sets a fresh JWKS response on the handler, publishing the test key's public half. <see cref="StringContent"/>
    /// is single-read, so a test that triggers a second real fetch (via <see cref="OpenEmrConfigurationManager.RequestRefresh"/>)
    /// must call this again before that second call, or the re-read throws.
    /// </summary>
    private void SetFreshJwksResponse()
    {
        var publicParameters = _rsa.ExportParameters(includePrivateParameters: false);
        var jwk = $"{{\"kty\":\"RSA\",\"n\":\"{Base64UrlEncoder.Encode(publicParameters.Modulus!)}\",\"e\":\"{Base64UrlEncoder.Encode(publicParameters.Exponent!)}\"}}";

        _handler.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"keys\":[{jwk}]}}", Encoding.UTF8, "application/json")
        };
    }

    /// <summary>A configuration manager over a fake JWKS endpoint publishing the test key's public half.</summary>
    private OpenEmrConfigurationManager CreateManager()
    {
        SetFreshJwksResponse();

        var configuration = new OpenIddictConfiguration
        {
            Issuer = new Uri("https://openemr.test/oauth2/default"),
            AuthorizationEndpoint = new Uri("https://openemr.test/oauth2/default/authorize"),
            TokenEndpoint = new Uri("https://openemr.test/oauth2/default/token")
        };

        return new OpenEmrConfigurationManager(new HttpClient(_handler), configuration, JwksUri);
    }

    [Fact]
    public async Task First_call_fetches_the_jwks_from_the_configured_uri()
    {
        var manager = CreateManager();

        await manager.GetConfigurationAsync(CancellationToken.None);

        Assert.Equal(JwksUri, _handler.Request!.RequestUri);
    }

    [Fact]
    public async Task Fetched_keys_populate_both_the_key_set_and_the_flat_signing_keys_list()
    {
        // Regression guard: token validation reads Configuration.SigningKeys, a flat list, not
        // JsonWebKeySet — populating only the key set was ID2090's second cause (ADR 0026).
        var manager = CreateManager();

        var configuration = await manager.GetConfigurationAsync(CancellationToken.None);

        Assert.NotNull(configuration.JsonWebKeySet);
        Assert.NotEmpty(configuration.JsonWebKeySet.Keys);
        Assert.NotEmpty(configuration.SigningKeys);
        Assert.IsType<RsaSecurityKey>(configuration.SigningKeys[0]);
    }

    [Fact]
    public async Task The_fixed_endpoints_pass_through_unchanged()
    {
        var manager = CreateManager();

        var configuration = await manager.GetConfigurationAsync(CancellationToken.None);

        Assert.Equal(new Uri("https://openemr.test/oauth2/default"), configuration.Issuer);
        Assert.Equal(new Uri("https://openemr.test/oauth2/default/authorize"), configuration.AuthorizationEndpoint);
        Assert.Equal(new Uri("https://openemr.test/oauth2/default/token"), configuration.TokenEndpoint);
    }

    [Fact]
    public async Task A_second_call_within_the_cache_lifetime_does_not_refetch()
    {
        var manager = CreateManager();
        await manager.GetConfigurationAsync(CancellationToken.None);

        await manager.GetConfigurationAsync(CancellationToken.None);

        Assert.Equal(1, _handler.CallCount);
    }

    [Fact]
    public async Task RequestRefresh_forces_the_next_call_to_refetch()
    {
        var manager = CreateManager();
        await manager.GetConfigurationAsync(CancellationToken.None);

        manager.RequestRefresh();
        SetFreshJwksResponse();
        await manager.GetConfigurationAsync(CancellationToken.None);

        Assert.Equal(2, _handler.CallCount);
    }

    [Fact]
    public async Task Signing_keys_are_replaced_not_accumulated_across_refreshes()
    {
        var manager = CreateManager();
        await manager.GetConfigurationAsync(CancellationToken.None);

        manager.RequestRefresh();
        SetFreshJwksResponse();
        var configuration = await manager.GetConfigurationAsync(CancellationToken.None);

        Assert.Single(configuration.SigningKeys);
    }
}
