namespace OpenEmrNext.AuthFacade.Application.Abstractions;

/// <summary>Issues this service's own access tokens.</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Creates a signed access token for an authenticated user.</summary>
    /// <param name="subject">The user's stable identifier, placed in the <c>sub</c> claim.</param>
    /// <param name="name">The login name, placed in the <c>name</c> claim.</param>
    /// <returns>The serialized token and its lifetime in seconds.</returns>
    IssuedAccessToken Issue(string subject, string name);
}

/// <summary>A serialized access token and its lifetime.</summary>
/// <param name="AccessToken">The compact JWT string.</param>
/// <param name="ExpiresInSeconds">Seconds until the token expires, as reported to the client.</param>
public sealed record IssuedAccessToken(string AccessToken, int ExpiresInSeconds);
