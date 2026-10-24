namespace OpenEmrNext.AuthFacade.Api.Contracts.Responses;

/// <summary>The body of <c>GET /api/v1/me</c>: the caller's subject and name claims.</summary>
/// <param name="Subject">The <c>sub</c> claim of the bearer token.</param>
/// <param name="Name">The <c>name</c> claim of the bearer token.</param>
public sealed record MeResponse(string? Subject, string? Name);
