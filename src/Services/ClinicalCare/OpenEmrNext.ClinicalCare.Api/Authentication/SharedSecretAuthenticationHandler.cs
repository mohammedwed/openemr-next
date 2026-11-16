using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OpenEmrNext.ClinicalCare.Api.Authentication;

/// <summary>
/// Authenticates a trusted internal caller that presents a shared secret in one named header. One implementation of the
/// constant-time comparison (SEC-3) for every secret-gated scheme, so a fix lands once. Each subclass names its header
/// and says where its secret comes from; a request carrying the *other* scheme's secret authenticates as nobody here.
/// </summary>
internal abstract class SharedSecretAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected abstract string HeaderName { get; }
    protected abstract string ExpectedSecret { get; }

    protected sealed override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No credential at all: NoResult, so the authorization layer answers 401 (SEC-18 tests both cases).
        if (!Request.Headers.TryGetValue(HeaderName, out var presented) || presented.Count != 1)
            return Task.FromResult(AuthenticateResult.NoResult());

        // Fixed-time comparison: response-time differences must not reveal the secret byte by byte.
        var expected = Encoding.UTF8.GetBytes(ExpectedSecret);
        var actual = Encoding.UTF8.GetBytes(presented[0] ?? "");
        if (actual.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(actual, expected))
            return Task.FromResult(AuthenticateResult.Fail("The shared secret is not valid."));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
