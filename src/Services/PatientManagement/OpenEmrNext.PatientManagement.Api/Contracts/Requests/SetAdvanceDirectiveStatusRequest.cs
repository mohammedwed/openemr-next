namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>Body of <c>PUT /api/v1/patients/{pid}/advance-directives/status</c>. <c>Status</c> is
/// "yes", "no", or "unknown" (the blank "SKIP" choice legacy's own edit screen allows).</summary>
public sealed record SetAdvanceDirectiveStatusRequest(string Status, DateTime? ReviewedAtUtc, long? AuthenticatorUserId);
