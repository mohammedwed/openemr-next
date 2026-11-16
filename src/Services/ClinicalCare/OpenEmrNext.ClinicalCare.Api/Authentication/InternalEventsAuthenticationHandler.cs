using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpenEmrNext.ClinicalCare.Composition;

namespace OpenEmrNext.ClinicalCare.Api.Authentication;

/// <summary>
/// Authenticates an event producer, presenting the secret configured by <see cref="InternalEventsOptions"/> as
/// <c>X-Internal-Events-Secret</c>. Separate from the gateway's scheme on purpose (API-16): the producer can deliver
/// events but cannot call the public API, and the gateway can call the public API but cannot deliver events.
/// </summary>
internal sealed class InternalEventsAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IOptionsMonitor<InternalEventsOptions> eventsOptions)
    : SharedSecretAuthenticationHandler(options, logger, encoder)
{
    /// <summary>The scheme name this handler is registered under.</summary>
    public const string SchemeName = "InternalEvents";

    protected override string HeaderName => "X-Internal-Events-Secret";
    protected override string ExpectedSecret => eventsOptions.CurrentValue.Secret;
}
