namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>The body of the unauthenticated <c>GET /api/health</c> smoke-test endpoint.</summary>
/// <param name="Message">A fixed greeting.</param>
public sealed record HelloResponse(string Message);
