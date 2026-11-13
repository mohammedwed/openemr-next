using System.Net.Mail;

namespace OpenEmrNext.PatientManagement.Api.Domain;

/// <summary>
/// A registered patient. <see cref="Id"/>/<see cref="Pid"/>/<see cref="Pubpid"/> are database-generated
/// (ADR 0049) and are unset until <see cref="AssignGeneratedIdentity"/> runs after the insert -- they are
/// never chosen here, unlike OpenEMR's own <c>getFreshPid()</c> (docs/m4-domain-notes.md's race
/// condition), which this aggregate deliberately does not reproduce.
/// </summary>
public sealed class Patient
{
    public long Id { get; private set; }
    public long Pid { get; private set; }
    public string Pubpid { get; private set; } = "";
    public Guid Uuid { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string Sex { get; }
    public DateOnly DateOfBirth { get; }
    public string? Email { get; }
    public string? TrustedEmail { get; }
    public DateTime RegisteredAtUtc { get; }
    public long RegisteredBy { get; }

    private Patient(PatientRegistration registration, long registeredBy, DateTime registeredAtUtc)
    {
        FirstName = registration.FirstName;
        LastName = registration.LastName;
        Sex = registration.Sex;
        DateOfBirth = registration.DateOfBirth;
        Email = registration.Email;
        TrustedEmail = registration.TrustedEmail;
        RegisteredBy = registeredBy;
        RegisteredAtUtc = registeredAtUtc;
        Uuid = Guid.NewGuid();
    }

    /// <summary>
    /// Validates <paramref name="registration"/> against the same required/optional rules OpenEMR's own
    /// <c>PatientValidator::DATABASE_INSERT_CONTEXT</c> enforces (docs/m4-domain-notes.md) and, if valid,
    /// returns a new <see cref="Patient"/> with no identity yet assigned.
    /// </summary>
    /// <exception cref="PatientValidationException">One or more fields fail validation.</exception>
    public static Patient Register(PatientRegistration registration, long registeredBy, DateTime registeredAtUtc)
    {
        var errors = new List<string>();

        if (registration.FirstName.Length is < 1 or > 255)
            errors.Add("First name must be between 1 and 255 characters.");

        if (registration.LastName.Length is < 2 or > 255)
            errors.Add("Last name must be between 2 and 255 characters.");

        if (registration.Sex.Length is < 4 or > 30)
            errors.Add("Gender must be between 4 and 30 characters.");

        if (registration.Email is { Length: > 0 } email && !IsValidEmail(email))
            errors.Add("Email is not a valid email.");

        if (registration.TrustedEmail is { Length: > 0 } trustedEmail && !IsValidEmail(trustedEmail))
            errors.Add("Trusted Email is not a valid email.");

        if (errors.Count > 0)
            throw new PatientValidationException(errors);

        return new Patient(registration, registeredBy, registeredAtUtc);
    }

    /// <summary>
    /// Called once, by the persistence layer, right after the database assigns <paramref name="id"/> via
    /// its own <c>AUTO_INCREMENT</c> on insert. <see cref="Pid"/> is set to the same value --
    /// <c>patient_data</c>'s child tables join on <c>pid</c>, not <c>id</c> (docs/m2-schema-inventory.md),
    /// but nothing requires it to be sequential or "fresh" the way OpenEMR's own code assumed; it only
    /// needs to be unique, which a value copied from an already-atomic <c>AUTO_INCREMENT</c> guarantees
    /// for free. <see cref="Pubpid"/> defaults to the same value, matching OpenEMR's own default when no
    /// public ID is supplied at registration (docs/m4-domain-notes.md).
    /// </summary>
    internal void AssignGeneratedIdentity(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException($"Patient {Uuid} already has an assigned identity.");

        Id = id;
        Pid = id;
        Pubpid = id.ToString();
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
