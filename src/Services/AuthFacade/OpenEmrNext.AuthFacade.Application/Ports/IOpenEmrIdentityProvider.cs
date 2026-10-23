using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Ports;

/// <summary>Verifies credentials against the legacy OpenEMR identity server.</summary>
public interface IOpenEmrIdentityProvider
{
    /// <summary>Submits a password grant to OpenEMR and reports whether it accepted the credentials.</summary>
    /// <param name="username">The login name to verify.</param>
    /// <param name="password">The password to verify. Its plaintext is consumed by the call.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>OpenEMR's status and body, plus the subject when the grant succeeded.</returns>
    /// <exception cref="IdentityProviderUnavailableException">OpenEMR could not be reached.</exception>
    /// <exception cref="IdentityProviderResponseException">OpenEMR succeeded but returned no usable token.</exception>
    Task<PasswordGrantResult> PasswordGrantAsync(Username username, Password password, CancellationToken ct);
}

/// <summary>The outcome of a password grant sent to OpenEMR.</summary>
/// <param name="StatusCode">HTTP status returned by OpenEMR.</param>
/// <param name="Body">OpenEMR's response body, passed through unchanged on rejection.</param>
/// <param name="Subject">The authenticated subject; set only when OpenEMR accepted the credentials.</param>
public sealed record PasswordGrantResult(int StatusCode, string Body, string? Subject);

/// <summary>Raised when the identity provider cannot be reached.</summary>
public sealed class IdentityProviderUnavailableException(Exception innerException)
    : Exception("OpenEMR is unreachable.", innerException);

/// <summary>Raised when OpenEMR answers successfully but the response is not a usable token.</summary>
public sealed class IdentityProviderResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
