using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenEmrNext.PatientManagement.Composition;

namespace OpenEmrNext.PatientManagement.Api.Authentication;

/// <summary>
/// Authenticates a trusted internal caller presenting the shared secret configured by
/// <see cref="InternalGatewayOptions"/> as the <c>X-Internal-Gateway-Secret</c> header. This gates the
/// service shut to anyone else; it does not resolve or verify which end user is behind the call --
/// callers still identify the acting user via the request's own fields, exactly as before.
/// </summary>
internal sealed class InternalGatewayAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<InternalGatewayOptions> gatewayOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The scheme name this handler is registered under.</summary>
    public const string SchemeName = "InternalGateway";

    private const string SecretHeaderName = "X-Internal-Gateway-Secret";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SecretHeaderName, out var presented) || presented.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Fixed-time comparison: a caller close enough to see response-time differences should not be
        // able to learn the secret one byte at a time from how fast a mismatch gets rejected.
        var expected = Encoding.UTF8.GetBytes(gatewayOptions.CurrentValue.Secret);
        var actual = Encoding.UTF8.GetBytes(presented[0] ?? "");
        if (actual.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("The internal gateway secret is not valid."));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
