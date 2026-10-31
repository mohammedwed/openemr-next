using System.Security.Cryptography;
using System.Text;

namespace OpenEmrNext.AuthFacade.Api.BrowserFlow;

/// <summary>Generates the random tokens and PKCE challenge the <c>/login</c> endpoint sends to OpenEMR.</summary>
internal static class Pkce
{
    /// <summary>
    /// A cryptographically random, URL-safe token of <paramref name="byteLength"/> bytes. Used for
    /// <c>state</c>, <c>nonce</c> and the PKCE <c>code_verifier</c>.
    /// </summary>
    public static string GenerateToken(int byteLength = 32) => Base64Url(RandomNumberGenerator.GetBytes(byteLength));

    /// <summary>Computes the S256 PKCE <c>code_challenge</c> for a <c>code_verifier</c>.</summary>
    public static string ComputeChallenge(string codeVerifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    /// <summary>Base64url with no padding (RFC 7636 §A), dependency-free.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
