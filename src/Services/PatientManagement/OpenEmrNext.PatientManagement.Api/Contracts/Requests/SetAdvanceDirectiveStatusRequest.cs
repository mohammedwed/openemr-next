using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>Body of <c>PUT /api/v1/patients/{pid}/advance-directives/status</c>. <c>Status</c> is
/// "yes", "no", or "unknown" (the blank "SKIP" choice legacy's own edit screen allows).</summary>
/// <param name="AuthenticatorUserId">The id of the user who reviewed the advance directive.
/// Caller-supplied for now, not verified against any authenticated session -- an already-accepted gap
/// inherited from ADR 0048 (no authentication wired into this service yet). Revisit once that gap
/// closes. Unlike <c>RegisteredBy</c>/<c>OwnerId</c> elsewhere, nothing in Domain or Application
/// validated this id at all until <c>[Range]</c> was added here -- legacy defines no rule linking it
/// to <paramref name="Status"/>'s value, but "a positive id when one is given" is still a reasonable,
/// cheap floor.</param>
public sealed record SetAdvanceDirectiveStatusRequest(
    [Required(ErrorMessage = "Status is required.")] string Status,
    DateTime? ReviewedAtUtc,
    [Range(1, long.MaxValue, ErrorMessage = "AuthenticatorUserId must identify the reviewing user.")] long? AuthenticatorUserId);
