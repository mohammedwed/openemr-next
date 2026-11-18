using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Api.Contracts.Requests;

/// <summary>
/// The demographics of a patient, as they should now be (M5r, ADR 0081): the same six fields registration takes, under the same rules. It is a <b>replacement</b> of those six: a field left out, or an email left empty,
/// clears it (the other 120-odd columns of the patient are never touched). Sent with the <c>ETag</c> the caller read in <c>If-Match</c>.
/// </summary>
public sealed record UpdatePatientRequest(
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
    [EmailAddress(ErrorMessage = "Trusted Email is not a valid email.")] string? TrustedEmail);
