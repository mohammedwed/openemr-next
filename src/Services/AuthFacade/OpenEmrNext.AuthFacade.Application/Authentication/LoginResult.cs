using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

/// <summary>The result of a password login attempt. The endpoint maps each case to an HTTP response.</summary>
public abstract record LoginResult;

/// <summary>OpenEMR accepted the credentials and its access token verified.</summary>
/// <param name="Token">The verified token fields to return to the caller.</param>
public sealed record LoginSucceeded(OpenEmrToken Token) : LoginResult;

/// <summary>OpenEMR refused the credentials, for example with a 400 <c>invalid_grant</c>.</summary>
public sealed record LoginRejected : LoginResult;

/// <summary>OpenEMR could not be reached.</summary>
public sealed record LoginUnavailable : LoginResult;

/// <summary>OpenEMR failed, returned an unusable success response, or issued a token that does not verify.</summary>
public sealed record LoginUpstreamError : LoginResult;
