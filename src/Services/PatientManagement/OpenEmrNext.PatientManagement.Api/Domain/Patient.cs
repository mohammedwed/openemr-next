using System.Net.Mail;

namespace OpenEmrNext.PatientManagement.Api.Domain;

/// <summary>
/// A registered patient. <see cref="Id"/> is the one value that's genuinely database-generated (ADR
/// 0049) -- EF Core's own <c>ValueGeneratedOnAdd()</c> populates it directly after insert (via the
/// field access <c>PatientManagementDbContext</c> configures), so application code never assigns it
/// itself. <see cref="Pid"/>/<see cref="Pubpid"/> are generated here instead, eagerly, the same way
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
    public long Id { get; }
    public long Pid { get; } = 0;
    public string Pubpid { get; } = null!;
    public Guid Uuid { get; }
    public string FirstName { get; } = null!;
    public string LastName { get; } = null!;
    public string Sex { get; } = null!;
    public DateOnly DateOfBirth { get; }

    // Backed by their own fields, not auto-properties: the real email/email_direct columns are NOT NULL
    // DEFAULT '' (no nullable representation exists in the database at all), so EF maps these two fields
    // directly (PatientManagementDbContext's HasField) as plain non-null strings, while the properties
    // above present the nicer string? to application code. A ValueConverter looked like the obvious fix
    // but EF skips running a converter entirely when the source value is null by default -- confirmed
    // the hard way, via a NOT NULL constraint violation against a real database -- so this sidesteps
    // that pitfall rather than fighting it.
    private readonly string _email = "";
    private readonly string _trustedEmail = "";
    public string? Email => string.IsNullOrEmpty(_email) ? null : _email;
    public string? TrustedEmail => string.IsNullOrEmpty(_trustedEmail) ? null : _trustedEmail;

    public DateTime RegisteredAtUtc { get; }
    public long RegisteredBy { get; }

    /// <summary>EF Core's own materialization path only -- reads from the database via backing fields
    /// (see <c>PatientManagementDbContext</c>'s <c>UsePropertyAccessMode(Field)</c>), never application
    /// code. <see cref="Register"/> is the only way application code ever creates a valid instance.</summary>
    private Patient()
    {
    }

    private Patient(PatientRegistration registration, long registeredBy, DateTime registeredAtUtc)
    {
        FirstName = registration.FirstName;
        LastName = registration.LastName;
        Sex = registration.Sex;
        DateOfBirth = registration.DateOfBirth;
        _email = registration.Email ?? "";
        _trustedEmail = registration.TrustedEmail ?? "";
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
    /// already assigned. <see cref="Id"/> stays <c>0</c> until the database assigns it on insert.
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
