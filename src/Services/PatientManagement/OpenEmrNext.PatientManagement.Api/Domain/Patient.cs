using System.Net.Mail;

namespace OpenEmrNext.PatientManagement.Api.Domain;

/// <summary>
/// A registered patient. <see cref="Id"/> is the one value that's genuinely database-generated (ADR
/// 0049) and stays unset until <see cref="AssignGeneratedIdentity"/> runs after the insert.
/// <see cref="Pid"/>/<see cref="Pubpid"/> are generated here instead, eagerly, the same way
/// <see cref="Uuid"/> already is: <c>pid</c> is <c>NOT NULL DEFAULT 0</c> with a <c>UNIQUE KEY</c>
/// (docs/m2-schema-inventory.md), so setting it only after an insert (<c>Pid = Id</c>, requiring a
/// second write once <c>Id</c> is known) would leave two concurrent registrations both briefly holding
/// the default <c>0</c> at once -- exactly the kind of race OpenEMR's own <c>getFreshPid()</c>
/// (docs/m4-domain-notes.md) already illustrates the cost of. <c>Pid</c> only needs to be unique, not
/// sequential or derived from <c>Id</c>, so it is generated independently and never needs that second
/// write at all.
/// </summary>
public sealed class Patient
{
    public long Id { get; private set; }
    public long Pid { get; }
    public string Pubpid { get; }
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
        Pid = Random.Shared.NextInt64(1, long.MaxValue);
        Pubpid = Pid.ToString();
    }

    /// <summary>
    /// Validates <paramref name="registration"/> against the same required/optional rules OpenEMR's own
    /// <c>PatientValidator::DATABASE_INSERT_CONTEXT</c> enforces (docs/m4-domain-notes.md) and, if valid,
    /// returns a new <see cref="Patient"/> with <see cref="Uuid"/>/<see cref="Pid"/>/<see cref="Pubpid"/>
    /// already assigned. Only <see cref="Id"/> waits for <see cref="AssignGeneratedIdentity"/>.
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
    /// its own <c>AUTO_INCREMENT</c> on insert. <see cref="Pid"/>/<see cref="Pubpid"/> are already set
    /// (see the constructor) and are untouched here.
    /// </summary>
    internal void AssignGeneratedIdentity(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException($"Patient {Uuid} already has an assigned identity.");

        Id = id;
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
