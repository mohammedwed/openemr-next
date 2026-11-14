using System.Net.Mail;
using OpenEmrNext.PatientManagement.Domain;

namespace OpenEmrNext.PatientManagement.Domain.ValueObjects;

/// <summary>
/// The fields a caller supplies to register a new patient. Mirrors the required/optional split
/// OpenEMR's own <c>PatientValidator::DATABASE_INSERT_CONTEXT</c> enforces (docs/m4-domain-notes.md),
/// not reusing its column names directly -- <c>Sex</c> here is the same free-text "Gender" field, kept
/// under the legacy column's own name rather than the validator's UI label, since no enum exists to
/// rename it to.
/// </summary>
/// <remarks>
/// Validates on construction: <see cref="Create"/> is the only way to get an instance, so
/// <see cref="Patient.Register"/> never has to re-check what's already guaranteed true of a
/// <see cref="PatientRegistration"/> it receives -- one owner of the rule, not two places that could
/// drift apart.
/// </remarks>
public sealed record PatientRegistration
{
    public string FirstName { get; }
    public string LastName { get; }
    public string Sex { get; }
    public DateOnly DateOfBirth { get; }
    public string? Email { get; }
    public string? TrustedEmail { get; }

    private PatientRegistration(
        string firstName, string lastName, string sex, DateOnly dateOfBirth, string? email, string? trustedEmail)
    {
        FirstName = firstName;
        LastName = lastName;
        Sex = sex;
        DateOfBirth = dateOfBirth;
        Email = email;
        TrustedEmail = trustedEmail;
    }

    /// <exception cref="PatientValidationException">One or more fields fail validation.</exception>
    public static PatientRegistration Create(
        string firstName, string lastName, string sex, DateOnly dateOfBirth,
        string? email = null, string? trustedEmail = null)
    {
        var errors = new List<string>();

        if (firstName.Length is < 1 or > 255)
            errors.Add("First name must be between 1 and 255 characters.");

        if (lastName.Length is < 2 or > 255)
            errors.Add("Last name must be between 2 and 255 characters.");

        if (sex.Length is < 4 or > 30)
            errors.Add("Gender must be between 4 and 30 characters.");

        if (email is { Length: > 0 } && !IsValidEmail(email))
            errors.Add("Email is not a valid email.");

        if (trustedEmail is { Length: > 0 } && !IsValidEmail(trustedEmail))
            errors.Add("Trusted Email is not a valid email.");

        if (errors.Count > 0)
            throw new PatientValidationException(errors);

        return new PatientRegistration(firstName, lastName, sex, dateOfBirth, email, trustedEmail);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
