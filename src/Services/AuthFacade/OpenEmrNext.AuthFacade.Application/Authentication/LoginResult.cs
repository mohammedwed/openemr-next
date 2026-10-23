namespace OpenEmrNext.AuthFacade.Application.Authentication;

/// <summary>The result of a password login attempt. The endpoint maps each case to an HTTP response.</summary>
public abstract record LoginResult;

/// <summary>OpenEMR accepted the credentials. Its token response is returned to the caller unchanged.</summary>
/// <param name="Body">OpenEMR's token response body.</param>
public sealed record LoginSucceeded(string Body) : LoginResult;

/// <summary>OpenEMR rejected the request; its status and body are returned to the caller.</summary>
public sealed record LoginRejected(int StatusCode, string Body) : LoginResult;

/// <summary>OpenEMR could not be reached.</summary>
public sealed record LoginUnavailable : LoginResult;

/// <summary>OpenEMR answered with a success status but no usable token.</summary>
public sealed record LoginUpstreamError : LoginResult;
