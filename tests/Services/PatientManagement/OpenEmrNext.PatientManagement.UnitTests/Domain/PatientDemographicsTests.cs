using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ReadModels;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain;

/// <summary>The pure parts of correcting a patient (M5r, ADR 0081): the version, the rules a correction shares with registration, and what counts as a change.</summary>
public class PatientDemographicsTests
{
    private static readonly Guid Uuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");

    private static PatientDemographicsView Stored(
        string first = "Ada", string last = "Lovelace", string sex = "Female", DateOnly? dob = null, string? email = "ada@example.com", string? trusted = null) =>
        new(Uuid, 42, first, last, sex, dob ?? new DateOnly(1815, 12, 10), email, trusted);

    // The version

    [Fact]
    public void The_same_values_have_the_same_version_and_it_is_a_quoted_strong_tag_with_a_prefix_of_its_own()
    {
        Assert.Equal(Stored().Version().ToETag(), Stored().Version().ToETag());
        Assert.Matches("^\"p-[0-9a-f]{32}\"$", Stored().Version().ToETag());
    }

    [Theory]
    [InlineData("Augusta", "Lovelace", "Female", "ada@example.com", null)]
    [InlineData("Ada", "King", "Female", "ada@example.com", null)]
    [InlineData("Ada", "Lovelace", "Male", "ada@example.com", null)]
    [InlineData("Ada", "Lovelace", "Female", "other@example.com", null)]
    [InlineData("Ada", "Lovelace", "Female", "ada@example.com", "direct@example.com")]
    public void Changing_any_owned_value_changes_the_version(string first, string last, string sex, string? email, string? trusted)
    {
        Assert.NotEqual(Stored().Version().ToETag(), Stored(first, last, sex, email: email, trusted: trusted).Version().ToETag());
    }

    [Fact]
    public void A_different_date_of_birth_or_none_is_a_different_version()
    {
        Assert.NotEqual(Stored().Version().ToETag(), Stored(dob: new DateOnly(1815, 12, 11)).Version().ToETag());
        Assert.NotEqual(Stored().Version().ToETag(), new PatientDemographicsView(Uuid, 42, "Ada", "Lovelace", "Female", null, "ada@example.com", null).Version().ToETag());
    }

    [Fact]
    public void The_public_patient_id_and_the_patient_number_are_not_part_of_the_version()
    {
        var other = Stored() with { Pid = 99, PublicPatientId = "ABC-1" };

        Assert.Equal(Stored().Version().ToETag(), other.Version().ToETag());
    }

    [Fact]
    public void Values_cannot_be_slid_across_a_boundary_to_make_two_patients_share_a_version()
    {
        Assert.NotEqual(Stored(first: "Ab", last: "Cd").Version().ToETag(), Stored(first: "A", last: "bCd").Version().ToETag());
    }

    [Fact]
    public void An_empty_email_and_none_are_the_same_patient_and_so_the_same_version()
    {
        // The column is NOT NULL DEFAULT '': there is no "none" in the database to tell apart from an empty one.
        Assert.Equal(Stored(email: null).Version().ToETag(), Stored(email: "").Version().ToETag());
    }

    // Reading the tag

    [Fact]
    public void An_if_match_with_the_current_tag_among_others_yields_it_and_a_tag_of_another_shape_yields_nothing()
    {
        var tag = Stored().Version().ToETag();

        Assert.Equal([tag], PatientVersion.ParseIfMatch($"\"c-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\", {tag}"));
        Assert.Empty(PatientVersion.ParseIfMatch("\"c-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\""));
        Assert.Empty(PatientVersion.ParseIfMatch("\"p-short\""));
        Assert.Empty(PatientVersion.ParseIfMatch("\"p-zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz\""));
        Assert.Empty(PatientVersion.ParseIfMatch(null));
        Assert.Empty(PatientVersion.ParseIfMatch("   "));
    }

    [Fact]
    public void A_tag_is_read_whatever_its_case_and_compares_equal_to_the_one_issued()
    {
        var issued = Stored().Version().ToETag();

        Assert.Equal([issued], PatientVersion.ParseIfMatch(issued.ToUpperInvariant().Replace("\"P-", "\"p-")));
    }

    [Fact]
    public void A_default_version_has_no_tag()
    {
        Assert.Throws<InvalidOperationException>(() => default(PatientVersion).ToETag());
    }

    // The rules are registration's

    [Theory]
    [InlineData("", "Lovelace", "Female", "ada@example.com")]
    [InlineData("Ada", "L", "Female", null)]
    [InlineData("Ada", "Lovelace", "F", null)]
    [InlineData("Ada", "Lovelace", "Female", "not an email")]
    public void A_correction_refuses_exactly_what_registration_refuses_with_the_same_words(string first, string last, string sex, string? email)
    {
        var onRegister = Assert.Throws<PatientValidationException>(() => PatientRegistration.Create(first, last, sex, new DateOnly(1815, 12, 10), email, null));
        var onCorrect = Assert.Throws<PatientValidationException>(() => PatientDemographics.Create(first, last, sex, new DateOnly(1815, 12, 10), email, null));

        Assert.Equal(onRegister.Errors, onCorrect.Errors);
    }

    [Fact]
    public void An_empty_email_is_no_email()
    {
        var d = PatientDemographics.Create("Ada", "Lovelace", "Female", new DateOnly(1815, 12, 10), "", "");

        Assert.Null(d.Email);
        Assert.Null(d.TrustedEmail);
    }

    // What counts as a change

    [Fact]
    public void Nothing_changed_is_no_names()
    {
        var same = PatientDemographics.Create("Ada", "Lovelace", "Female", new DateOnly(1815, 12, 10), "ada@example.com", null);

        Assert.Empty(Stored().ChangedBy(same));
    }

    [Fact]
    public void Each_changed_field_is_named_in_a_stable_order_and_a_value_is_never_named()
    {
        var all = PatientDemographics.Create("Augusta", "King", "Male", new DateOnly(1900, 1, 1), "new@example.com", "direct@example.com");

        Assert.Equal(["firstName", "lastName", "sex", "dateOfBirth", "email", "trustedEmail"], Stored().ChangedBy(all));
    }

    [Fact]
    public void A_legacy_row_registration_would_not_have_admitted_is_still_compared_as_it_is()
    {
        // A one-letter surname and a missing date of birth: registration's rules would refuse them, a correction must still be able to read them and say what it changes.
        var legacy = new PatientDemographicsView(Uuid, 42, "Ada", "L", "Female", null, null, null);
        var fixedUp = PatientDemographics.Create("Ada", "Lovelace", "Female", new DateOnly(1815, 12, 10), null, null);

        Assert.Equal(["lastName", "dateOfBirth"], legacy.ChangedBy(fixedUp));
    }

    [Fact]
    public void A_change_of_case_is_a_change()
    {
        Assert.Equal(["lastName"], Stored().ChangedBy(PatientDemographics.Create("Ada", "LOVELACE", "Female", new DateOnly(1815, 12, 10), "ada@example.com", null)));
    }
}
