namespace OpenEmrNext.AuthFacade.Application.Abstractions;

/// <summary>Issues this service's own access tokens.</summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(string subject, string name);
}

public sealed record IssuedAccessToken(string AccessToken, int ExpiresInSeconds);
