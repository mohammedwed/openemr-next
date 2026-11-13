namespace OpenEmrNext.PatientManagement.Api.Domain;

/// <summary>
/// The fields a caller supplies to register a new patient. Mirrors the required/optional split
/// OpenEMR's own <c>PatientValidator::DATABASE_INSERT_CONTEXT</c> enforces (docs/m4-domain-notes.md),
/// not reusing its column names directly -- <c>Sex</c> here is the same free-text "Gender" field, kept
/// under the legacy column's own name rather than the validator's UI label, since no enum exists to
/// rename it to.
/// </summary>
public sealed record PatientRegistration(
    string FirstName,
    string LastName,
    string Sex,
    DateOnly DateOfBirth,
    string? Email = null,
    string? TrustedEmail = null);
