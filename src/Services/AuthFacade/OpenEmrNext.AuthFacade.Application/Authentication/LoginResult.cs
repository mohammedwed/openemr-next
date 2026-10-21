using OpenEmrNext.AuthFacade.Application.Abstractions;

namespace OpenEmrNext.AuthFacade.Application.Authentication;

public abstract record LoginResult;

public sealed record LoginSucceeded(IssuedAccessToken Token) : LoginResult;

/// <summary>OpenEMR rejected the request; its status and body are returned to the caller.</summary>
public sealed record LoginRejected(int StatusCode, string Body) : LoginResult;

public sealed record LoginUnavailable : LoginResult;

/// <summary>OpenEMR answered with a success status but no usable token.</summary>
public sealed record LoginUpstreamError : LoginResult;
