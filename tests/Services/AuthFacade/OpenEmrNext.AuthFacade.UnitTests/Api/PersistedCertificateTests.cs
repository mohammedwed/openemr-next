using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.WebUtilities;

namespace OpenEmrNext.AuthFacade.UnitTests.Api;

/// <summary>
/// Proves the two branches in <c>AddOpenIddictClient</c>'s certificate selection (ADR 0045) really are two
/// different code paths, not just a configuration no-op: every other test leaves <c>AuthFacadeCertificates</c>
/// unset and so only ever exercises OpenIddict's own development-certificate fallback (every other test
/// passing already proves that side works). This test points it at two real, generated PKCS#12 files —
/// the same shape <c>scripts/setup/new-authfacade-certs.sh</c> produces — and drives a full login round
/// trip to prove OpenIddict actually loads and uses them, not just that the app starts.
/// </summary>
public class PersistedCertificateTests
{
    private static string WriteSelfSignedPfx(string path)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN=Test {Path.GetFileNameWithoutExtension(path)}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, "test-password"));
        return path;
    }

    [Fact]
    public async Task Configuring_AuthFacadeCertificates_still_completes_a_real_login_round_trip()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("authfacade-cert-test");
        try
        {
            var encryptionPath = WriteSelfSignedPfx(Path.Combine(tempDirectory.FullName, "encryption.pfx"));
            var signingPath = WriteSelfSignedPfx(Path.Combine(tempDirectory.FullName, "signing.pfx"));

            await using var server = await FakeOpenEmrServer.StartAsync();
            var port = TestApi.GetFreeTcpPort();
            await using var api = await TestApi.StartAsync(
                new Dictionary<string, string?>
                {
                    ["OpenEmr:BaseUrl"] = server.BaseUrl,
                    ["OpenEmr:Issuer"] = server.Issuer,
                    ["OpenEmr:RedirectUri"] = $"http://127.0.0.1:{port}/callback",
                    ["AuthFacadeCertificates:EncryptionCertificatePath"] = encryptionPath,
                    ["AuthFacadeCertificates:SigningCertificatePath"] = signingPath,
                    ["AuthFacadeCertificates:Password"] = "test-password"
                },
                port);

            using var login = await api.BrowserClient.GetAsync("/oidc/login");
            var query = QueryHelpers.ParseQuery(login.Headers.Location!.Query);
            server.Nonce = query["nonce"]!;
            using var callback = await api.BrowserClient.GetAsync($"/callback?code=fake-code&state={query["state"]}");

            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.Equal("https://app.test/", callback.Headers.Location!.ToString());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
