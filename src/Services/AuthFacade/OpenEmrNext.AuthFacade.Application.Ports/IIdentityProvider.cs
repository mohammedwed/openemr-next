using OpenEmrNext.AuthFacade.Domain.ValueObjects;

namespace OpenEmrNext.AuthFacade.Application.Ports;

/// <summary>Verifies credentials against the legacy OpenEMR identity server.</summary>
public interface IIdentityProvider
{
    /// <summary>Submits a password grant to OpenEMR and reports whether it accepted the credentials.</summary>
    /// <param name="username">The login name to verify.</param>
    /// <param name="password">The password to verify. Its plaintext is consumed by the call.</param>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>OpenEMR's status, plus the token when OpenEMR accepted the grant. The token is unverified.</returns>
    /// <exception cref="IdentityProviderUnavailableException">OpenEMR could not be reached.</exception>
    /// <exception cref="IdentityProviderResponseException">OpenEMR succeeded but returned no usable token.</exception>
    Task<PasswordGrantResult> PasswordGrantAsync(Username username, Password password, CancellationToken ct);
}

/// <summary>The outcome of a password grant sent to OpenEMR.</summary>
/// <param name="StatusCode">HTTP status returned by OpenEMR.</param>
/// <param name="Token">The token from a successful response. Null otherwise. It must be verified before use.</param>
public sealed record PasswordGrantResult(int StatusCode, OpenEmrToken? Token = null);

/// <summary>The token fields AuthFacade takes from OpenEMR's success response. The access token is unverified.</summary>
/// <param name="AccessToken">The <c>access_token</c>. Non-empty.</param>
/// <param name="TokenType">The <c>token_type</c>, <c>Bearer</c> when OpenEMR omits it.</param>
/// <param name="ExpiresIn">The <c>expires_in</c> in seconds, when OpenEMR returns it.</param>
/// <param name="RefreshToken">The <c>refresh_token</c>, when OpenEMR issues one.</param>
public sealed record OpenEmrToken(string AccessToken, string TokenType, int? ExpiresIn, string? RefreshToken);

/// <summary>Raised when the identity provider cannot be reached.</summary>
public sealed class IdentityProviderUnavailableException(Exception innerException)
    : Exception("OpenEMR is unreachable.", innerException);

/// <summary>Raised when OpenEMR answers successfully but the response is not a usable token.</summary>
public sealed class IdentityProviderResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
