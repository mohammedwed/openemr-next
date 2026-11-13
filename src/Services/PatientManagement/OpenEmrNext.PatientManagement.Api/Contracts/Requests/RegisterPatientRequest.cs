namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/patients</c>.
/// </summary>
/// <param name="FirstName">The patient's first name.</param>
/// <param name="LastName">The patient's last name.</param>
/// <param name="Sex">Free-text, matching OpenEMR's own <c>sex</c> column (docs/m4-domain-notes.md) -- no fixed list exists to validate against.</param>
/// <param name="DateOfBirth">The patient's date of birth.</param>
/// <param name="Email">Optional. Validated for shape if present.</param>
/// <param name="TrustedEmail">Optional. Validated for shape if present.</param>
/// <param name="RegisteredBy">
/// The id of the user performing the registration. Caller-supplied for now, not verified against any
/// authenticated session -- this service has no authentication wired into it yet (an already-accepted
/// gap inherited from ADR 0048: the gateway validates no tokens yet, and nothing downstream of it does
/// either). Revisit once that gap closes.
/// </param>
public sealed record RegisterPatientRequest(
    string FirstName,
    string LastName,
    string Sex,
    DateOnly DateOfBirth,
    string? Email,
    string? TrustedEmail,
    long RegisteredBy);
