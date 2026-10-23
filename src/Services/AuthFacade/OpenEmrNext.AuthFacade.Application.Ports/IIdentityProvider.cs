using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Ports;

/// <summary>Verifies credentials against the legacy OpenEMR identity server.</summary>
public interface IIdentityProvider
{
    /// <summary>Submits a password grant to OpenEMR and reports whether it accepted the credentials.</summary>
    /// <param name="username">The login name to verify.</param>
    /// <param name="password">The password to verify. Its plaintext is consumed by the call.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>OpenEMR's status and body, unchanged, plus the access token when OpenEMR accepted the grant.</returns>
    /// <exception cref="IdentityProviderUnavailableException">OpenEMR could not be reached.</exception>
    /// <exception cref="IdentityProviderResponseException">OpenEMR succeeded but returned no usable token.</exception>
    Task<PasswordGrantResult> PasswordGrantAsync(Username username, Password password, CancellationToken ct);
}

/// <summary>The outcome of a password grant sent to OpenEMR.</summary>
/// <param name="StatusCode">HTTP status returned by OpenEMR.</param>
/// <param name="Body">OpenEMR's response body. It is returned to the caller unchanged.</param>
/// <param name="AccessToken">The <c>access_token</c> from a successful response. Null otherwise. It must be verified before use.</param>
public sealed record PasswordGrantResult(int StatusCode, string Body, string? AccessToken = null);

/// <summary>Raised when the identity provider cannot be reached.</summary>
public sealed class IdentityProviderUnavailableException(Exception innerException)
    : Exception("OpenEMR is unreachable.", innerException);

/// <summary>Raised when OpenEMR answers successfully but the response is not a usable token.</summary>
public sealed class IdentityProviderResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
