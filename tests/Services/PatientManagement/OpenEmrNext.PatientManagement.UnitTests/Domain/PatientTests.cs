using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

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
        PatientRegistration.Create(firstName, lastName, sex, dob ?? SomeDob, email, trustedEmail);

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
    public void Register_leaves_Id_at_zero_since_only_the_database_assigns_it()
    {
        var patient = Patient.Register(ValidRegistration(), registeredBy: 1, Now);

        Assert.Equal(0, patient.Id);
        Assert.NotEqual(0, patient.Pid);
        Assert.Equal(patient.Pid.ToString(), patient.Pubpid);
    }

    [Fact]
    public void Register_generates_a_different_Pid_each_time()
    {
        var first = Patient.Register(ValidRegistration(), 1, Now);
        var second = Patient.Register(ValidRegistration(), 1, Now);

        Assert.NotEqual(first.Pid, second.Pid);
    }

    [Fact]
    public void Register_accepts_no_email_at_all()
    {
        var patient = Patient.Register(ValidRegistration(email: null, trustedEmail: null), 1, Now);
        Assert.Null(patient.Email);
        Assert.Null(patient.TrustedEmail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Register_rejects_a_registeredBy_that_cannot_identify_a_user(long registeredBy)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => Patient.Register(ValidRegistration(), registeredBy, Now));
        Assert.Contains(ex.Errors, e => e.Contains("RegisteredBy"));
    }
}
