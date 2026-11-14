using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.ValueObjects;

/// <summary>Validation used to live on <see cref="Patient.Register"/>; it moved to
/// <see cref="PatientRegistration.Create"/> so a <see cref="PatientRegistration"/> can never exist in
/// an invalid state in the first place, rather than relying on every caller to re-check it.</summary>
public class PatientRegistrationTests
{
    private static readonly DateOnly SomeDob = new(1990, 1, 1);

    [Theory]
    [InlineData("")]
    public void Create_rejects_an_empty_first_name(string firstName)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => PatientRegistration.Create(firstName, "Lovelace", "Female", SomeDob));
        Assert.Contains(ex.Errors, e => e.Contains("First name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    public void Create_rejects_a_last_name_under_two_characters(string lastName)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => PatientRegistration.Create("Ada", lastName, "Female", SomeDob));
        Assert.Contains(ex.Errors, e => e.Contains("Last name"));
    }

    [Theory]
    [InlineData("M")]
    [InlineData("F")]
    public void Create_rejects_a_gender_under_four_characters(string sex)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => PatientRegistration.Create("Ada", "Lovelace", sex, SomeDob));
        Assert.Contains(ex.Errors, e => e.Contains("Gender"));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    public void Create_rejects_a_malformed_email(string email)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => PatientRegistration.Create("Ada", "Lovelace", "Female", SomeDob, email: email));
        Assert.Contains(ex.Errors, e => e.Contains("Email"));
    }

    [Theory]
    [InlineData("not-an-email")]
    public void Create_rejects_a_malformed_trusted_email(string trustedEmail)
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => PatientRegistration.Create("Ada", "Lovelace", "Female", SomeDob, trustedEmail: trustedEmail));
        Assert.Contains(ex.Errors, e => e.Contains("Trusted Email"));
    }

    [Fact]
    public void Create_reports_every_failing_field_at_once()
    {
        var ex = Assert.Throws<PatientValidationException>(
            () => PatientRegistration.Create("", "A", "M", SomeDob));

        Assert.Equal(3, ex.Errors.Count);
    }
}
