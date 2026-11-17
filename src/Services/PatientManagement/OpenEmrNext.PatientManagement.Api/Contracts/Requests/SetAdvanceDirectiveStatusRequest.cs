using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>Body of <c>PUT /api/v1/patients/{pid}/advance-directives/status</c>. <c>Status</c> is
/// "yes", "no", or "unknown" (the blank "SKIP" choice legacy's own edit screen allows).</summary>
/// <param name="AuthenticatorUserId">Ignored: the acting user is the signed-in user the gateway verified (ADR 0075), whatever this says. Kept optional so existing callers do not break; it will be removed in a later contract version.</param>
public sealed record SetAdvanceDirectiveStatusRequest(
    [Required(ErrorMessage = "Status is required.")] string Status,
    DateTime? ReviewedAtUtc,
    [Range(1, long.MaxValue, ErrorMessage = "AuthenticatorUserId must identify the reviewing user.")] long? AuthenticatorUserId);
