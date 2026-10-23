namespace OpenEmrNext.AuthFacade.Application.Ports;

/// <summary>Verifies a token issued by OpenEMR before this service trusts it.</summary>
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
/// <param name="Name">The <c>name</c> claim, or null when the issuer does not supply one.</param>
public sealed record VerifiedToken(string Subject, string? Name);

/// <summary>Raised when the keys needed to verify an external token cannot be fetched.</summary>
public sealed class TokenVerifierUnavailableException(Exception innerException)
    : Exception("The token issuer's keys are unavailable.", innerException);
