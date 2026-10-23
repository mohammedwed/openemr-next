namespace OpenEmrNext.AuthFacade.Application.Ports;

/// <summary>Verifies a token issued by an external identity provider before this service trusts it.</summary>
public interface IExternalTokenVerifier
{
    /// <summary>Checks the token's signature, issuer, audience, algorithm and lifetime.</summary>
    /// <param name="token">The compact JWT presented by the client.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The verified subject, or null when the token is not valid.</returns>
    /// <exception cref="TokenVerifierUnavailableException">The keys needed to check the token could not be fetched.</exception>
    Task<VerifiedToken?> VerifyAsync(string token, CancellationToken ct);
}

/// <summary>The claims this service takes from a verified external token.</summary>
/// <param name="Subject">The <c>sub</c> claim: the user's stable identifier at the issuer.</param>
public sealed record VerifiedToken(string Subject);

/// <summary>Raised when the keys needed to verify an external token cannot be fetched.</summary>
public sealed class TokenVerifierUnavailableException(Exception innerException)
    : Exception("The token issuer's keys are unavailable.", innerException);
