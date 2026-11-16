using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenEmrNext.ClinicalCare.Composition;

namespace OpenEmrNext.ClinicalCare.Api.Authentication;

/// <summary>
/// Authenticates the gateway, presenting the secret configured by <see cref="InternalGatewayOptions"/> as
/// <c>X-Internal-Gateway-Secret</c> (ADR 0055 pattern). Gates the public API shut to anyone else; it does not verify
/// which end user is behind the call.
/// </summary>
internal sealed class InternalGatewayAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IOptionsMonitor<InternalGatewayOptions> gatewayOptions)
    : SharedSecretAuthenticationHandler(options, logger, encoder)
{
    /// <summary>The scheme name this handler is registered under.</summary>
    public const string SchemeName = "InternalGateway";

    protected override string HeaderName => "X-Internal-Gateway-Secret";
    protected override string ExpectedSecret => gatewayOptions.CurrentValue.Secret;
}
