using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>
/// The body of <c>POST /api/v1/patients</c>. The bounds here mirror
/// <see cref="OpenEmrNext.PatientManagement.Domain.ValueObjects.PatientRegistration.Create"/>'s own
/// rules exactly -- restating them here lets a malformed request fail at the model-binding boundary,
/// before any use case or Domain factory runs, but the Domain factory remains the one place that
/// actually enforces them; these attributes exist to fail the same checks earlier, not to replace them.
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
    [Required(ErrorMessage = "First name must be between 1 and 255 characters.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "First name must be between 1 and 255 characters.")]
    string FirstName,
    [Required(ErrorMessage = "Last name must be between 2 and 255 characters.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 255 characters.")]
    string LastName,
    [Required(ErrorMessage = "Gender must be between 4 and 30 characters.")]
    [StringLength(30, MinimumLength = 4, ErrorMessage = "Gender must be between 4 and 30 characters.")]
    string Sex,
    DateOnly DateOfBirth,
    [EmailAddress(ErrorMessage = "Email is not a valid email.")] string? Email,
    [EmailAddress(ErrorMessage = "Trusted Email is not a valid email.")] string? TrustedEmail,
    [Range(1, long.MaxValue, ErrorMessage = "RegisteredBy must identify the registering user.")] long RegisteredBy);
