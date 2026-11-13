using OpenEmrNext.PatientManagement.Api.Domain;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain;

public class PatientTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly SomeDob = new(1990, 1, 1);

    private static PatientRegistration ValidRegistration(
        string firstName = "Ada",
        string lastName = "Lovelace",
        string sex = "Female",
        DateOnly? dob = null,
        string? email = null,
        string? trustedEmail = null) =>
        new(firstName, lastName, sex, dob ?? SomeDob, email, trustedEmail);

    [Fact]
    public void Register_with_valid_fields_succeeds()
    {
        var patient = Patient.Register(ValidRegistration(), registeredBy: 1, Now);

        Assert.Equal("Ada", patient.FirstName);
        Assert.Equal("Lovelace", patient.LastName);
        Assert.Equal("Female", patient.Sex);
        Assert.Equal(SomeDob, patient.DateOfBirth);
        Assert.Equal(1, patient.RegisteredBy);
        Assert.Equal(Now, patient.RegisteredAtUtc);
        Assert.NotEqual(Guid.Empty, patient.Uuid);
    }

    [Fact]
    public void Register_leaves_no_identity_assigned_until_AssignGeneratedIdentity_runs()
    {
        var patient = Patient.Register(ValidRegistration(), registeredBy: 1, Now);

        Assert.Equal(0, patient.Id);
        Assert.Equal(0, patient.Pid);
        Assert.Equal("", patient.Pubpid);
    }

    [Theory]
    [InlineData("")]
    public void Register_rejects_an_empty_first_name(string firstName)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(firstName: firstName), 1, Now));
        Assert.Contains(ex.Errors, e => e.Contains("First name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Register_rejects_a_last_name_under_two_characters(string lastName)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(lastName: lastName), 1, Now));
        Assert.Contains(ex.Errors, e => e.Contains("Last name"));
    }

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    public void Register_rejects_a_gender_under_four_characters(string sex)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(sex: sex), 1, Now));
        Assert.Contains(ex.Errors, e => e.Contains("Gender"));
    }

    [Fact]
    public void Register_accepts_no_email_at_all()
    {
        var patient = Patient.Register(ValidRegistration(email: null, trustedEmail: null), 1, Now);
        Assert.Null(patient.Email);
        Assert.Null(patient.TrustedEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Register_rejects_a_malformed_email(string email)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(email: email), 1, Now));
        Assert.Contains(ex.Errors, e => e.Contains("Email"));
    }

    [Theory]
    [InlineData("not-an-email")]
    public void Register_rejects_a_malformed_trusted_email(string trustedEmail)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(trustedEmail: trustedEmail), 1, Now));
        Assert.Contains(ex.Errors, e => e.Contains("Trusted Email"));
    }

    [Fact]
    public void Register_reports_every_failing_field_at_once()
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(firstName: "", lastName: "A", sex: "M"), 1, Now));

        Assert.Equal(3, ex.Errors.Count);
    }

    [Fact]
    public void AssignGeneratedIdentity_sets_pid_and_pubpid_to_the_same_value_as_id()
    {
        var patient = Patient.Register(ValidRegistration(), 1, Now);

        patient.AssignGeneratedIdentity(42);

        Assert.Equal(42, patient.Id);
        Assert.Equal(42, patient.Pid);
        Assert.Equal("42", patient.Pubpid);
    }

    [Fact]
    public void AssignGeneratedIdentity_cannot_run_twice()
    {
        var patient = Patient.Register(ValidRegistration(), 1, Now);
        patient.AssignGeneratedIdentity(42);

        Assert.Throws<InvalidOperationException>(() => patient.AssignGeneratedIdentity(43));
    }
}
