namespace OpenEmrNext.PatientManagement.Domain.ValueObjects;

/// <summary>
/// The demographics of a patient that this service owns and lets a user correct (ADR 0081): the six fields registration writes. The rules are registration's own, applied by the same code, so a patient can never be edited
/// into a state they could not have been registered in; a rule is changed in one place.
/// </summary>
public sealed record PatientDemographics
{
    public string FirstName { get; }
    public string LastName { get; }
    public string Sex { get; }
    public DateOnly DateOfBirth { get; }
    public string? Email { get; }
    public string? TrustedEmail { get; }

    private PatientDemographics(PatientRegistration valid)
    {
        FirstName = valid.FirstName;
        LastName = valid.LastName;
        Sex = valid.Sex;
        DateOfBirth = valid.DateOfBirth;
        Email = string.IsNullOrEmpty(valid.Email) ? null : valid.Email;
        TrustedEmail = string.IsNullOrEmpty(valid.TrustedEmail) ? null : valid.TrustedEmail;
    }

    /// <exception cref="PatientValidationException">A field fails the rules registration applies; every failing field is named.</exception>
    public static PatientDemographics Create(string firstName, string lastName, string sex, DateOnly dateOfBirth, string? email, string? trustedEmail) =>
        new(PatientRegistration.Create(firstName, lastName, sex, dateOfBirth, email, trustedEmail));
}
