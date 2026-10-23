using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Api.Authentication;

/// <summary>
/// Authenticates a <c>Bearer</c> credential that OpenEMR issued. The token is verified by
/// <see cref="IExternalTokenVerifier"/> against OpenEMR's keys, and the verified claims become the principal.
/// </summary>
internal sealed class OpenEmrBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IExternalTokenVerifier verifier)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var token = header[BearerPrefix.Length..].Trim();

        VerifiedToken? verified;
        try
        {
            verified = await verifier.VerifyAsync(token, Context.RequestAborted);
        }
        catch (TokenVerifierUnavailableException)
        {
            // The keys cannot be fetched, so the token cannot be checked. It is refused, not trusted.
            return AuthenticateResult.Fail("The token issuer's keys are unavailable.");
        }

        if (verified is null)
        {
            return AuthenticateResult.Fail("The token is not valid.");
        }

        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, verified.Subject) };
        if (verified.Name is { } name)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, name));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
