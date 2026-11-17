using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Immunizations;

/// <summary>The rules of an immunization (IM-1 to IM-8, docs/clinical-care/14-immunizations-research.md), tested with no database or framework (DOM-19).</summary>
public class ImmunizationFactsTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 9, 30, 0);
    private const long Pid = 42;

    private static readonly ImmunizationCodeData Codes = new(
        new HashSet<int> { 6, 7, 27 }, new HashSet<string> { "intramuscular", "bymouth" }, new HashSet<string> { "left_arm", "right_arm" },
        new HashSet<string> { "Completed", "Refused" }, new HashSet<string> { "00", "01" }, new HashSet<string> { "OSTUDY", "PATOBJ" },
        new HashSet<string> { "1", "9" });

    private static ImmunizationInput Valid() => new(new DateTime(2026, 11, 10, 8, 15, 30, 400), ImmunizationId: 6);

    private static ImmunizationFacts Create(ImmunizationInput input) => ImmunizationFacts.Create(input, Pid, Now, Codes);

    private static IReadOnlyList<string> ErrorsOf(ImmunizationInput input) =>
        Assert.Throws<ClinicalCareValidationException>(() => Create(input)).Errors;

    [Fact]
    public void A_minimal_immunization_is_valid_and_its_time_is_cut_to_whole_seconds()
    {
        var facts = Create(Valid());

        Assert.Equal(new DateTime(2026, 11, 10, 8, 15, 30), facts.AdministeredDate);
        Assert.Equal(6, facts.ImmunizationId);
        Assert.False(facts.EnteredInError);
    }

    [Fact]
    public void Every_field_of_a_full_immunization_is_kept_in_its_stored_form()
    {
        var facts = Create(new ImmunizationInput(
            Valid().AdministeredDate, 6, " 140 ", " Acme ", " L123 ", new DateTime(2027, 5, 1, 13, 0, 0), 0.50004m, "1", "intramuscular", "left_arm", "Completed",
            "00", null, new DateTime(2026, 11, 10, 7, 0, 0), new DateTime(2026, 8, 6), " Nurse Joy ", 7, 9, new ImmunizationEncounter(1001, Pid), " R1 ", "why", "note", true));

        Assert.Equal("140", facts.CvxCode);
        Assert.Equal("Acme", facts.Manufacturer);
        Assert.Equal("L123", facts.LotNumber);
        Assert.Equal(new DateTime(2027, 5, 1), facts.ExpirationDate);
        Assert.Equal(0.5000m, facts.AmountAdministered);
        Assert.Equal("1", facts.AmountUnit);
        Assert.Equal("intramuscular", facts.Route);
        Assert.Equal("left_arm", facts.AdministrationSite);
        Assert.Equal("Completed", facts.CompletionStatus);
        Assert.Equal("00", facts.InformationSource);
        Assert.Equal(new DateTime(2026, 11, 10), facts.EducationDate);
        Assert.Equal(new DateTime(2026, 8, 6), facts.VisDate);
        Assert.Equal("Nurse Joy", facts.AdministeredBy);
        Assert.Equal(7, facts.AdministeredById);
        Assert.Equal(9, facts.OrderingProvider);
        Assert.Equal(1001, facts.EncounterNumber);
        Assert.Equal("R1", facts.ReasonCode);
        Assert.Equal("why", facts.ReasonDescription);
        Assert.Equal("note", facts.Note);
        Assert.True(facts.EnteredInError);
    }

    // IM-1
    [Fact]
    public void An_administered_date_is_required() =>
        Assert.Contains("An administered date is required.", ErrorsOf(Valid() with { AdministeredDate = null }));

    [Theory]
    [InlineData(1899, 12, 31)]
    [InlineData(2026, 11, 19)]
    public void An_administered_date_before_1900_or_after_tomorrow_is_refused(int y, int m, int d) =>
        Assert.Contains(ErrorsOf(Valid() with { AdministeredDate = new DateTime(y, m, d) }), e => e.StartsWith("Administered date must be between", StringComparison.Ordinal));

    [Fact]
    public void A_date_a_few_hours_ahead_is_allowed_to_cover_zones() =>
        Assert.NotNull(Create(Valid() with { AdministeredDate = Now.AddHours(5) }));

    // IM-2
    [Fact]
    public void The_vaccine_must_be_identified_by_a_code_or_a_cvx_code()
    {
        Assert.Contains(ErrorsOf(new ImmunizationInput(Valid().AdministeredDate)), e => e.StartsWith("Identify the vaccine", StringComparison.Ordinal));
        Assert.NotNull(Create(new ImmunizationInput(Valid().AdministeredDate, CvxCode: "140")));
        Assert.Contains(ErrorsOf(new ImmunizationInput(Valid().AdministeredDate, CvxCode: "  ")), e => e.StartsWith("Identify the vaccine", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_vaccine_code_that_is_not_configured_is_refused(int code) =>
        Assert.Contains($"Immunization {code} is not a known immunization.", ErrorsOf(Valid() with { ImmunizationId = code }));

    [Fact]
    public void A_cvx_code_wider_than_its_column_is_refused() =>
        Assert.Contains("CVX code must be at most 64 characters.", ErrorsOf(Valid() with { CvxCode = new string('1', 65) }));

    // IM-3
    [Theory]
    [InlineData("Route")]
    [InlineData("Administration site")]
    [InlineData("Completion status")]
    [InlineData("Information source")]
    [InlineData("Refusal reason")]
    [InlineData("Amount unit")]
    public void A_code_that_is_not_configured_is_refused_and_the_match_is_exact(string field)
    {
        var input = field switch
        {
            "Route" => Valid() with { Route = "INTRAMUSCULAR" },
            "Administration site" => Valid() with { AdministrationSite = "left arm" },
            "Completion status" => Valid() with { CompletionStatus = "completed" },
            "Information source" => Valid() with { InformationSource = "99" },
            "Refusal reason" => Valid() with { RefusalReason = "nope" },
            _ => Valid() with { AmountAdministered = 1m, AmountUnit = "42" }
        };

        Assert.Contains(ErrorsOf(input), e => e.StartsWith($"{field} '", StringComparison.Ordinal) && e.Contains("is not a known", StringComparison.Ordinal));
    }

    [Fact]
    public void A_blank_code_means_none()
    {
        var facts = Create(Valid() with { Route = "  ", CompletionStatus = "" });

        Assert.Null(facts.Route);
        Assert.Null(facts.CompletionStatus);
    }

    // IM-4
    [Theory]
    [InlineData("Manufacturer", 101)]
    [InlineData("Lot number", 51)]
    [InlineData("Administered by", 256)]
    [InlineData("Reason code", 32)]
    public void Text_wider_than_its_column_is_refused(string label, int length)
    {
        var text = new string('x', length);
        var input = label switch
        {
            "Manufacturer" => Valid() with { Manufacturer = text },
            "Lot number" => Valid() with { LotNumber = text },
            "Administered by" => Valid() with { AdministeredBy = text },
            _ => Valid() with { ReasonCode = text }
        };

        Assert.Contains(ErrorsOf(input), e => e.StartsWith($"{label} must be at most", StringComparison.Ordinal));
    }

    [Fact]
    public void Text_exactly_as_wide_as_its_column_is_accepted()
    {
        var facts = Create(Valid() with { Manufacturer = new string('m', 100), LotNumber = new string('l', 50), AdministeredBy = new string('a', 255), ReasonCode = new string('r', 31) });

        Assert.Equal(100, facts.Manufacturer!.Length);
    }

    [Fact]
    public void The_two_text_fields_are_limited_in_bytes_not_characters()
    {
        var multiByte = new string('é', 40_000);   // 40,000 characters, 80,000 bytes: fits a character count, not the column

        var errors = ErrorsOf(Valid() with { Note = multiByte, ReasonDescription = multiByte });

        Assert.Contains("Note must be at most 65535 bytes.", errors);
        Assert.Contains("Reason description must be at most 65535 bytes.", errors);
        Assert.NotNull(Create(Valid() with { Note = new string('é', 32_767) }));   // 65,534 bytes
    }

    // IM-5
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void An_amount_outside_the_plausible_range_is_refused(double amount) =>
        Assert.Contains(ErrorsOf(Valid() with { AmountAdministered = (decimal)amount, AmountUnit = "1" }), e => e.StartsWith("Amount administered must be", StringComparison.Ordinal));

    [Fact]
    public void An_amount_needs_a_unit() =>
        Assert.Contains("An amount needs a unit.", ErrorsOf(Valid() with { AmountAdministered = 0.5m }));

    [Fact]
    public void An_amount_is_rounded_to_four_places_away_from_zero() =>
        Assert.Equal(0.1235m, Create(Valid() with { AmountAdministered = 0.12345m, AmountUnit = "1" }).AmountAdministered);

    // IM-6
    [Fact]
    public void An_expired_vaccine_is_recorded_not_refused()
    {
        var facts = Create(Valid() with { ExpirationDate = new DateTime(2026, 1, 1) });

        Assert.Equal(new DateTime(2026, 1, 1), facts.ExpirationDate);
    }

    [Theory]
    [InlineData("Education date")]
    [InlineData("VIS date")]
    [InlineData("Expiration date")]
    public void An_implausible_date_is_refused(string label)
    {
        var tooEarly = new DateTime(1899, 1, 1);
        var input = label switch
        {
            "Education date" => Valid() with { EducationDate = tooEarly },
            "VIS date" => Valid() with { VisDate = tooEarly },
            _ => Valid() with { ExpirationDate = tooEarly }
        };

        Assert.Contains(ErrorsOf(input), e => e.StartsWith($"{label} must be between", StringComparison.Ordinal));
    }

    [Fact]
    public void An_education_or_vis_date_in_the_future_is_refused_but_an_expiration_may_be_years_ahead()
    {
        Assert.Contains(ErrorsOf(Valid() with { VisDate = Now.AddDays(30) }), e => e.StartsWith("VIS date must be between", StringComparison.Ordinal));
        Assert.NotNull(Create(Valid() with { ExpirationDate = Now.AddYears(10) }));
        Assert.Contains(ErrorsOf(Valid() with { ExpirationDate = Now.AddYears(60) }), e => e.StartsWith("Expiration date must be between", StringComparison.Ordinal));
    }

    // Users and encounters
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData((long)int.MaxValue + 1)]
    public void Provider_ids_must_fit_their_columns(long id)
    {
        var errors = ErrorsOf(Valid() with { AdministeredById = id, OrderingProvider = id });

        Assert.Contains("Administered-by id must identify a user.", errors);
        Assert.Contains("Ordering provider must identify a user.", errors);
    }

    // IM-7
    [Fact]
    public void A_linked_encounter_must_belong_to_the_same_patient() =>
        Assert.Contains("The encounter belongs to a different patient than the immunization.", ErrorsOf(Valid() with { Encounter = new ImmunizationEncounter(1001, 99) }));

    [Fact]
    public void A_linked_encounter_must_have_a_number() =>
        Assert.Contains("The linked encounter has no encounter number.", ErrorsOf(Valid() with { Encounter = new ImmunizationEncounter(0, Pid) }));

    [Fact]
    public void Every_violation_is_reported_together()
    {
        var errors = ErrorsOf(new ImmunizationInput(
            null, ImmunizationId: 99, Manufacturer: new string('x', 200), AmountAdministered: 0m, Route: "bad", EducationDate: new DateTime(1800, 1, 1)));

        Assert.True(errors.Count >= 5, string.Join(" | ", errors));
    }
}
